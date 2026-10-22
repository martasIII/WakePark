using System.Net.Http.Json;
using System.Text.Json;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Testcontainers.PostgreSql;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Infrastructure.Outbox;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Infrastructure.Persistence;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Infrastructure.Persistence;
using WakePark.Modules.Notifications.Application.Emails;
using WakePark.Modules.Notifications.Infrastructure.Persistence;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Application.Payments;
using WakePark.Modules.Billing.Domain.Payments;
using WakePark.Modules.Vouchers.Infrastructure.Persistence;

namespace WakePark.Api.IntegrationTests;

/// <summary>
/// Objednávka → outbox Billingu → Notifications → SMTP, celé proti skutečnému PostgreSQL
/// a Mailpitu. Workery na pozadí jsou vypnuté, jednotlivá kola se spouštějí ručně —
/// test tak řídí, kdy se co stane.
/// </summary>
public sealed class OrderEmailFlowTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.27")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/livez")))
        .Build();

    private readonly List<BankTransactionData> _payments = [];

    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mailpit.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:WakeParkDb", _postgres.GetConnectionString());
            builder.UseSetting("Notifications:PublicBaseUrl", "https://novy.wake-dzban.cz");
            builder.UseSetting("Notifications:Smtp:Host", _mailpit.Hostname);
            builder.UseSetting("Notifications:Smtp:Port", _mailpit.GetMappedPublicPort(1025).ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Notifications:Smtp:UseStartTls", "false");
            builder.UseSetting("Notifications:Smtp:Username", "");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                // Banka nahrazená seznamem plateb, který test plní sám.
                services.RemoveAll<IBankStatementSource>();
                services.AddSingleton<IBankStatementSource>(new FakeBank(_payments));
                // ARES nahrazený jednou známou firmou — test nesmí záviset na cizí službě.
                services.RemoveAll<ICompanyRegistry>();
                services.AddSingleton<ICompanyRegistry>(new FakeCompanyRegistry());
            });
            builder.UseSetting("Billing:Seller:Name", "Wake Park s.r.o.");
            builder.UseSetting("Billing:Seller:CompanyId", "06633633");
            builder.UseSetting("Billing:Seller:VatId", "CZ06633633");
            builder.UseSetting("Billing:Seller:Address", "Pavlovská 585/1, 162 00 Praha 6");
            builder.UseSetting("Billing:Seller:Registration", "C 285893 vedená u Městského soudu v Praze");
        });

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<VouchersDbContext>().Database.MigrateAsync();

        await scope.ServiceProvider
            .GetRequiredService<ICommandHandler<ImportCatalogCommand, CatalogImportResult>>()
            .HandleAsync(new ImportCatalogCommand(EnterpriseId, new CatalogDefinition(
                "CZK",
                [new CategoryDefinition("vouchery", new Dictionary<string, string> { ["cs"] = "Vouchery" })],
                [new ProductDefinition("voucher-1-slot", "vouchery", new Dictionary<string, string> { ["cs"] = "Voucher pro jednoho" },
                    new Dictionary<string, string> { ["cs"] = "20 minut" }, null, 690m, 12m)])));
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _mailpit.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Po_objednavce_odejde_jednou_potvrzeni_s_QR_a_odkazem()
    {
        using HttpClient client = _factory.CreateClient();
        CatalogDto catalog = (await client.GetFromJsonAsync<CatalogDto>(
            new Uri($"/api/v1/catalog/enterprises/{EnterpriseId}", UriKind.Relative), TestContext.Current.CancellationToken))!;

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/billing/enterprises/{EnterpriseId}/orders", UriKind.Relative),
            new PlaceOrderRequest("cs", new PlaceOrderCustomer("Jana Nováková", "jana@example.cz", null, null),
                [new PlaceOrderLine(catalog.Products.Single().Id, 1)], TermsAccepted: true),
            TestContext.Current.CancellationToken);
        PlacedOrderDto placed = (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestContext.Current.CancellationToken))!;

        // Outbox: událost se doručí jednou, druhé kolo už nemá co dělat.
        OutboxDispatcher<BillingDbContext> dispatcher = _factory.Services.GetRequiredService<OutboxDispatcher<BillingDbContext>>();
        (await dispatcher.DispatchPendingAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await dispatcher.DispatchPendingAsync(TestContext.Current.CancellationToken)).ShouldBe(0);

        EmailBatchResult sent;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            sent = await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<SendPendingEmailsCommand, EmailBatchResult>>()
                .HandleAsync(new SendPendingEmailsCommand(), TestContext.Current.CancellationToken);
        }

        sent.ShouldBe(new EmailBatchResult(Sent: 1, Failed: 0, Expired: 0));

        using HttpClient mailpit = new() { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
        using JsonDocument list = JsonDocument.Parse(await mailpit.GetStringAsync(new Uri("/api/v1/messages", UriKind.Relative), TestContext.Current.CancellationToken));
        list.RootElement.GetProperty("total").GetInt32().ShouldBe(1);

        JsonElement summary = list.RootElement.GetProperty("messages")[0];
        summary.GetProperty("Subject").GetString().ShouldBe($"Objednávka {placed.Number} — Wake Park Džbán");
        summary.GetProperty("To")[0].GetProperty("Address").GetString().ShouldBe("jana@example.cz");
        summary.GetProperty("From").GetProperty("Address").GetString().ShouldBe("noreply@wakeparksro.cz");

        using JsonDocument detail = JsonDocument.Parse(await mailpit.GetStringAsync(
            new Uri($"/api/v1/message/{summary.GetProperty("ID").GetString()}", UriKind.Relative), TestContext.Current.CancellationToken));
        detail.RootElement.GetProperty("ReplyTo")[0].GetProperty("Address").GetString().ShouldBe("info@wake-dzban.cz");
        detail.RootElement.GetProperty("HTML").GetString()!.ShouldContain($"https://novy.wake-dzban.cz/objednavka/{placed.AccessToken}");
        detail.RootElement.GetProperty("Inline").GetArrayLength().ShouldBe(1, "QR kód jako obrázek v těle e-mailu");
    }

    [Fact]
    public async Task Po_zaplaceni_prijde_e_mail_s_vouchery_a_dokladem_v_PDF()
    {
        using HttpClient client = _factory.CreateClient();
        CatalogDto catalog = (await client.GetFromJsonAsync<CatalogDto>(
            new Uri($"/api/v1/catalog/enterprises/{EnterpriseId}", UriKind.Relative), TestContext.Current.CancellationToken))!;

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/billing/enterprises/{EnterpriseId}/orders", UriKind.Relative),
            new PlaceOrderRequest("cs", new PlaceOrderCustomer("Jana Nováková", "jana@example.cz", null, "06633633"),
                [new PlaceOrderLine(catalog.Products.Single().Id, 2)], TermsAccepted: true),
            TestContext.Current.CancellationToken);
        PlacedOrderDto placed = (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestContext.Current.CancellationToken))!;

        // Platba dorazí — přesná částka, VS = číslo objednávky.
        _payments.Add(new BankTransactionData(
            "777001", DateOnly.FromDateTime(DateTime.UtcNow), 1380m, "CZK", placed.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "1234567890/0800", "Nováková Jana", null));

        BankSyncResult sync;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            sync = await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<SyncBankTransactionsCommand, BankSyncResult>>()
                .HandleAsync(new SyncBankTransactionsCommand(EnterpriseId, today.AddDays(-7), today), TestContext.Current.CancellationToken);
        }

        sync.Matched.ShouldBe(1);

        // Billing → (objednávka založena, zaplacena) → Notifications a Vouchers; Vouchers → Notifications.
        await _factory.Services.GetRequiredService<OutboxDispatcher<BillingDbContext>>().DispatchPendingAsync(TestContext.Current.CancellationToken);
        (await _factory.Services.GetRequiredService<OutboxDispatcher<VouchersDbContext>>().DispatchPendingAsync(TestContext.Current.CancellationToken))
            .ShouldBe(1);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            (await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<SendPendingEmailsCommand, EmailBatchResult>>()
                .HandleAsync(new SendPendingEmailsCommand(), TestContext.Current.CancellationToken))
                .Sent.ShouldBe(2, "potvrzení objednávky a vouchery");
        }

        using HttpClient mailpit = new() { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
        using JsonDocument list = JsonDocument.Parse(await mailpit.GetStringAsync(new Uri("/api/v1/messages", UriKind.Relative), TestContext.Current.CancellationToken));
        JsonElement paid = list.RootElement.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("Subject").GetString() == $"Vaše vouchery — objednávka {placed.Number}");

        using JsonDocument detail = JsonDocument.Parse(await mailpit.GetStringAsync(
            new Uri($"/api/v1/message/{paid.GetProperty("ID").GetString()}", UriKind.Relative), TestContext.Current.CancellationToken));
        string[] files = [.. detail.RootElement.GetProperty("Attachments").EnumerateArray().Select(a => a.GetProperty("FileName").GetString()!)];

        files.Count(f => f.StartsWith("voucher-WD-", StringComparison.Ordinal)).ShouldBe(2, "jeden voucher na kus");
        files.ShouldContain($"doklad-FV_E_{DateTime.UtcNow.Year}-000001.pdf");
        detail.RootElement.GetProperty("Text").GetString()!.ShouldContain("Platí od 1. 5. 2027 do 30. 9. 2027.");

        // PDF si test uloží, ať jde vzhled zkontrolovat očima.
        string outputDirectory = Path.Combine(Path.GetTempPath(), "wakepark-pdf");
        Directory.CreateDirectory(outputDirectory);
        foreach (JsonElement attachment in detail.RootElement.GetProperty("Attachments").EnumerateArray())
        {
            byte[] pdf = await mailpit.GetByteArrayAsync(
                new Uri($"/api/v1/message/{paid.GetProperty("ID").GetString()}/part/{attachment.GetProperty("PartID").GetString()}", UriKind.Relative),
                TestContext.Current.CancellationToken);
            pdf[..4].ShouldBe("%PDF"u8.ToArray());
            await File.WriteAllBytesAsync(Path.Combine(outputDirectory, attachment.GetProperty("FileName").GetString()!), pdf, TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeCompanyRegistry : ICompanyRegistry
    {
        public Task<RegisteredCompany?> FindAsync(string companyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(companyId == "06633633"
                ? new RegisteredCompany("06633633", "Wake Park s.r.o.", "CZ06633633", "Pavlovská 585/1, Liboc, 16200 Praha 6", null)
                : null);
    }

    private sealed class FakeBank(List<BankTransactionData> payments) : IBankStatementSource
    {
        public Task<IReadOnlyList<BankTransactionData>> GetIncomingAsync(DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BankTransactionData>>([.. payments]);
    }
}
