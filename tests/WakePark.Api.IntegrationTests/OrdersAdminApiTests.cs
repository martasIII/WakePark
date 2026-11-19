using System.Net;
using System.Net.Http.Json;

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
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Application.Payments;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Domain.Payments;
using WakePark.Modules.Billing.Infrastructure.Persistence;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Infrastructure.Persistence;
using WakePark.Modules.Identity.Infrastructure.Persistence;
using WakePark.Modules.Notifications.Infrastructure.Persistence;
using WakePark.Modules.Vouchers.Infrastructure.Persistence;

namespace WakePark.Api.IntegrationTests;

/// <summary>
/// Přehled objednávek v backoffice (ADR 0012): seznam, detail s platbou a poukazy, platby
/// k ručnímu vyřízení. Proti skutečnému PostgreSQL, objednávky a platby jdou celou cestou.
/// </summary>
public sealed class OrdersAdminApiTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private const string AdminEmail = "admin@example.cz";
    private const string AdminPassword = "Testovaci-heslo-123";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    private readonly List<BankTransactionData> _payments = [];

    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:WakeParkDb", _postgres.GetConnectionString());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IBankStatementSource>();
                services.AddSingleton<IBankStatementSource>(new FakeBank(_payments));
            });
        });

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<VouchersDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>()
            .SeedAsync(AdminEmail, AdminPassword, EnterpriseId, TestContext.Current.CancellationToken);

        await scope.ServiceProvider
            .GetRequiredService<ICommandHandler<ImportCatalogCommand, CatalogImportResult>>()
            .HandleAsync(new ImportCatalogCommand(EnterpriseId, new CatalogDefinition(
                "CZK",
                null,
                [new CategoryDefinition("vouchery", new Dictionary<string, string> { ["cs"] = "Vouchery" })],
                [new ProductDefinition("voucher-1-slot", "vouchery", new Dictionary<string, string> { ["cs"] = "Voucher pro jednoho" },
                    null, null, 990m, 12m, ["eshop"])])));
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Bez_prihlaseni_objednavky_videt_nejsou()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage list = await client.GetAsync(new Uri("/api/v1/billing/admin/orders", UriKind.Relative), TestContext.Current.CancellationToken);
        HttpResponseMessage payments = await client.GetAsync(new Uri("/api/v1/billing/admin/payments/unmatched", UriKind.Relative), TestContext.Current.CancellationToken);

        list.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        payments.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_vidi_seznam_detail_s_platbou_a_poukazy_a_nesparovane_platby()
    {
        using HttpClient client = CreateClient();
        PlacedOrderDto paid = await PlaceOrderAsync(client, "Jana Nováková", "jana@example.cz", 2, ["Pro Kristýnku", ""]);
        PlacedOrderDto waiting = await PlaceOrderAsync(client, "Petr Novák", "petr@example.cz", 1, []);

        // Jana zaplatí přesně, další platba nesedí částkou → k ručnímu vyřízení.
        _payments.Add(new BankTransactionData("p1", Today, 1980m, "CZK", Vs(paid), "1234567890/0800", "Nováková Jana", null));
        _payments.Add(new BankTransactionData("p2", Today, 500m, "CZK", Vs(waiting), "2222222222/0100", "Novák Petr", "omylem málo"));
        await SyncAndIssueVouchersAsync();

        await SignInAsync(client);

        OrderListDto all = await GetAsync<OrderListDto>(client, "/api/v1/billing/admin/orders");
        all.Total.ShouldBe(2);
        all.Items[0].Number.ShouldBe(waiting.Number, "nejnovější první");
        all.Items.Single(o => o.Number == paid.Number).Summary.ShouldBe("2× Voucher pro jednoho");

        OrderListDto onlyPaid = await GetAsync<OrderListDto>(client, "/api/v1/billing/admin/orders?status=Paid");
        onlyPaid.Items.Select(o => o.Number).ShouldBe([paid.Number]);

        OrderListDto byEmail = await GetAsync<OrderListDto>(client, "/api/v1/billing/admin/orders?q=PETR@");
        byEmail.Items.Select(o => o.Number).ShouldBe([waiting.Number]);

        OrderListDto byNumber = await GetAsync<OrderListDto>(client, $"/api/v1/billing/admin/orders?q={paid.Number}");
        byNumber.Items.Select(o => o.Number).ShouldBe([paid.Number]);

        OrderAdminDetailDto detail = await GetAsync<OrderAdminDetailDto>(client, $"/api/v1/billing/admin/orders/{paid.Number}");
        detail.Status.ShouldBe("Paid");
        detail.Customer.Email.ShouldBe("jana@example.cz");
        detail.Payment!.Amount.ShouldBe(1980m);
        detail.Payment.CounterpartyName.ShouldBe("Nováková Jana");
        detail.InvoiceNumber.ShouldNotBeNull();
        detail.Lines.Single().Dedications.ShouldBe(["Pro Kristýnku", ""]);
        detail.Vouchers.Count.ShouldBe(2);
        detail.Vouchers.Select(v => v.Dedication).ShouldBe(["Pro Kristýnku", null]);
        detail.AccessToken.ShouldBe(paid.AccessToken);

        IReadOnlyList<UnmatchedPaymentDto> unmatched = await GetAsync<IReadOnlyList<UnmatchedPaymentDto>>(client, "/api/v1/billing/admin/payments/unmatched");
        unmatched.Single().Amount.ShouldBe(500m);
        unmatched.Single().Message.ShouldBe("omylem málo");
    }

    [Fact]
    public async Task Neznamy_stav_vrati_422_a_neznama_objednavka_404()
    {
        using HttpClient client = CreateClient();
        await SignInAsync(client);

        HttpResponseMessage badStatus = await client.GetAsync(new Uri("/api/v1/billing/admin/orders?status=Smazano", UriKind.Relative), TestContext.Current.CancellationToken);
        HttpResponseMessage unknown = await client.GetAsync(new Uri("/api/v1/billing/admin/orders/999999", UriKind.Relative), TestContext.Current.CancellationToken);

        badStatus.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Akce (ADR 0014): objednávka si vezme cenu po slevě a drží ji i po smazání akce.</summary>
    [Fact]
    public async Task Objednavka_v_akci_ma_cenu_po_sleve_a_akce_ji_pozdeji_nezmeni()
    {
        using HttpClient client = CreateClient();
        await SignInAsync(client);
        string xsrf = await XsrfTokenAsync(client);

        CatalogAdminDto catalog = (await client.GetFromJsonAsync<CatalogAdminDto>(
            new Uri("/api/v1/catalog/admin", UriKind.Relative), TestContext.Current.CancellationToken))!;
        using HttpRequestMessage create = new(HttpMethod.Post, new Uri("/api/v1/catalog/admin/promotions", UriKind.Relative))
        {
            Content = JsonContent.Create(new PromotionRequest(
                new Dictionary<string, string> { ["cs"] = "Vánoční akce" }, catalog.Categories.Single().Id, 15, catalog.Today, catalog.Today)),
        };
        create.Headers.Add("X-XSRF-TOKEN", xsrf);
        catalog = (await (await client.SendAsync(create, TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<CatalogAdminDto>(TestContext.Current.CancellationToken))!;

        PlacedOrderDto order = await PlaceOrderAsync(client, "Jana Nováková", "jana@example.cz", 2, ["", ""]);

        using HttpRequestMessage remove = new(HttpMethod.Delete, new Uri($"/api/v1/catalog/admin/promotions/{catalog.Promotions.Single().Id}", UriKind.Relative));
        remove.Headers.Add("X-XSRF-TOKEN", xsrf);
        (await client.SendAsync(remove, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // 990 Kč − 15 % = 841,50 → 842 Kč za kus, 2 kusy.
        OrderAdminDetailDto detail = await GetAsync<OrderAdminDetailDto>(client, $"/api/v1/billing/admin/orders/{order.Number}");
        detail.TotalGross.ShouldBe(1684m);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static string Vs(PlacedOrderDto order) => order.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<PlacedOrderDto> PlaceOrderAsync(HttpClient client, string name, string email, int quantity, string[] dedications)
    {
        CatalogDto catalog = (await client.GetFromJsonAsync<CatalogDto>(
            new Uri($"/api/v1/catalog/enterprises/{EnterpriseId}", UriKind.Relative), TestContext.Current.CancellationToken))!;

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/billing/enterprises/{EnterpriseId}/orders", UriKind.Relative),
            new PlaceOrderRequest("cs", new PlaceOrderCustomer(name, email, null, null),
                [new PlaceOrderLine(catalog.Products.Single().Id, quantity, dedications.Length > 0 ? dedications : null)], TermsAccepted: true),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Druhá objednávka musí být „novější“ i při stejném čase v milisekundách.
        await Task.Delay(20, TestContext.Current.CancellationToken);
        return (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestContext.Current.CancellationToken))!;
    }

    /// <summary>Stáhne platby, spáruje je a doručí „zaplaceno“ do Vouchers — ty vystaví poukazy.</summary>
    private async Task SyncAndIssueVouchersAsync()
    {
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<SyncBankTransactionsCommand, BankSyncResult>>()
                .HandleAsync(new SyncBankTransactionsCommand(EnterpriseId, Today.AddDays(-7), Today), TestContext.Current.CancellationToken);
        }

        await _factory.Services.GetRequiredService<OutboxDispatcher<BillingDbContext>>().DispatchPendingAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    private static async Task SignInAsync(HttpClient client)
    {
        string token = await XsrfTokenAsync(client);
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/v1/identity/login", UriKind.Relative))
        {
            Content = JsonContent.Create(new { Email = AdminEmail, Password = AdminPassword }),
        };
        request.Headers.Add("X-XSRF-TOKEN", token);
        HttpResponseMessage login = await client.SendAsync(request, TestContext.Current.CancellationToken);
        login.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<string> XsrfTokenAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        string cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';', StringComparison.Ordinal)]);
    }

    // Přihlašovací cookie je Secure — přes http by ji klient zpátky neposlal.
    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private sealed class FakeBank(List<BankTransactionData> payments) : IBankStatementSource
    {
        public Task<IReadOnlyList<BankTransactionData>> GetIncomingAsync(DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BankTransactionData>>([.. payments]);
    }
}
