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
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();

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
            new PlaceOrderRequest("cs", new PlaceOrderCustomer("Jana Nováková", "jana@example.cz", null, null, null, null),
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
}
