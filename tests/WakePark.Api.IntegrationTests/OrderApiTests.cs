using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Infrastructure.Persistence;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Infrastructure.Persistence;

namespace WakePark.Api.IntegrationTests;

/// <summary>
/// Celé API přes HTTP proti skutečnému PostgreSQL — tak, jak ho volá web. Hlídá věci, které
/// testy jednotlivých modulů nevidí: middleware, směrování, serializaci, chybové odpovědi.
/// </summary>
public sealed class OrderApiTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Ne Development: tam by API při startu seedovalo identitu a katalog.
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:WakeParkDb", _container.GetConnectionString());
        });

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();

        await scope.ServiceProvider
            .GetRequiredService<ICommandHandler<ImportCatalogCommand, CatalogImportResult>>()
            .HandleAsync(new ImportCatalogCommand(EnterpriseId, new CatalogDefinition(
                "CZK",
                [new CategoryDefinition("vouchery", new Dictionary<string, string> { ["cs"] = "Vouchery" })],
                [new ProductDefinition(
                    "voucher-2-sloty",
                    "vouchery",
                    new Dictionary<string, string> { ["cs"] = "Voucher pro dva" },
                    new Dictionary<string, string> { ["cs"] = "2 × 20 minut" },
                    null,
                    1290m,
                    12m)])));

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Objednavka_projde_bez_antiforgery_hlavicky_a_jde_precist_podle_klice()
    {
        Guid productId = (await GetCatalogAsync()).Products.Single().Id;

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            new Uri($"/api/v1/billing/enterprises/{EnterpriseId}/orders", UriKind.Relative),
            Request(productId, quantity: 2),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        PlacedOrderDto placed = (await response.Content.ReadFromJsonAsync<PlacedOrderDto>(TestContext.Current.CancellationToken))!;
        placed.AccessToken.ShouldBe(placed.AccessToken.ToLowerInvariant(), "token jde do adresy, web ji převádí na malá písmena");

        OrderDto order = (await _client.GetFromJsonAsync<OrderDto>(
            new Uri($"/api/v1/billing/orders/{placed.AccessToken}", UriKind.Relative),
            TestContext.Current.CancellationToken))!;

        order.Number.ShouldBe(placed.Number);
        order.TotalGross.ShouldBe(2580m);
        order.Payment!.Spayd.ShouldContain($"*AM:2580.00*CC:CZK*X-VS:{placed.Number}");
    }

    [Fact]
    public async Task Neplatny_formular_vrati_400_s_textem_pro_zakaznika()
    {
        Guid productId = (await GetCatalogAsync()).Products.Single().Id;

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            new Uri($"/api/v1/billing/enterprises/{EnterpriseId}/orders", UriKind.Relative),
            Request(productId, quantity: 1) with { TermsAccepted = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("obchodními podmínkami");
    }

    [Fact]
    public async Task Neznamy_klic_objednavky_je_404()
    {
        HttpResponseMessage response = await _client.GetAsync(
            new Uri($"/api/v1/billing/orders/{new string('0', 64)}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Prihlaseni_do_backoffice_bez_antiforgery_hlavicky_dal_neprojde()
    {
        // Výjimka z CSRF ochrany je jen u objednávky — jinde musí platit dál.
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            new Uri("/api/v1/identity/login", UriKind.Relative),
            new { email = "admin@example.cz", password = "x" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("X-XSRF-TOKEN");
    }

    private async Task<CatalogDto> GetCatalogAsync() =>
        (await _client.GetFromJsonAsync<CatalogDto>(
            new Uri($"/api/v1/catalog/enterprises/{EnterpriseId}", UriKind.Relative),
            TestContext.Current.CancellationToken))!;

    private static PlaceOrderRequest Request(Guid productId, int quantity) =>
        new(
            "cs",
            new PlaceOrderCustomer("Jana Nováková", "jana@example.cz", null, null),
            [new PlaceOrderLine(productId, quantity)],
            TermsAccepted: true);
}
