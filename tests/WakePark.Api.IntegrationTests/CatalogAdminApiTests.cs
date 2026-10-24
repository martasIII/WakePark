using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Infrastructure.Persistence;
using WakePark.Modules.Identity.Infrastructure.Persistence;

namespace WakePark.Api.IntegrationTests;

/// <summary>
/// Správa katalogu přes HTTP tak, jak ji bude volat backoffice (ADR 0009): přihlášení cookie,
/// antiforgery hlavička, změna v katalogu — a hned ji vidí veřejný ceník i e-shop.
/// </summary>
public sealed class CatalogAdminApiTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private const string AdminEmail = "admin@example.cz";
    private const string AdminPassword = "Testovaci-heslo-123";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

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
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>()
            .SeedAsync(AdminEmail, AdminPassword, EnterpriseId, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Bez_prihlaseni_katalog_spravovat_nejde()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/v1/catalog/admin", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_zalozi_kategorii_produkt_a_cenu_a_web_je_hned_vidi()
    {
        using HttpClient client = CreateClient();
        await SignInAsync(client);

        CatalogAdminDto catalog = await SendAsync(client, HttpMethod.Post, "/api/v1/catalog/admin/categories",
            new CreateCategoryRequest("vouchery", null, Czech("Vouchery"), Czech("Pošleme e-mailem."), 0));
        Guid categoryId = catalog.Categories.Single().Id;

        catalog = await SendAsync(client, HttpMethod.Post, "/api/v1/catalog/admin/products",
            new CreateProductRequest("voucher-1-slot", categoryId, Czech("Voucher pro jednoho"), null, null, null, 0,
                new ProductVisibilityDto(PriceList: true, Eshop: true, Pos: true)));
        Guid productId = catalog.Products.Single().Id;

        catalog = await SendAsync(client, HttpMethod.Put, $"/api/v1/catalog/admin/products/{productId}/price",
            new SetPriceRequest(690m, 12m, null));

        catalog.Products.Single().CurrentPrice!.UnitPrice.ShouldBe(690m);

        CatalogDto eshop = (await client.GetFromJsonAsync<CatalogDto>(
            new Uri($"/api/v1/catalog/enterprises/{EnterpriseId}", UriKind.Relative), TestContext.Current.CancellationToken))!;
        eshop.Products.Single().UnitPrice.ShouldBe(690m);

        PublicPriceListDto priceList = (await client.GetFromJsonAsync<PublicPriceListDto>(
            new Uri($"/api/v1/catalog/enterprises/{EnterpriseId}/price-list", UriKind.Relative), TestContext.Current.CancellationToken))!;
        priceList.Sections.Single().Description!["cs"].ShouldBe("Pošleme e-mailem.");
    }

    [Fact]
    public async Task Porusene_pravidlo_vrati_422_a_neznamy_produkt_404()
    {
        using HttpClient client = CreateClient();
        await SignInAsync(client);
        await SendAsync(client, HttpMethod.Post, "/api/v1/catalog/admin/categories",
            new CreateCategoryRequest("vouchery", null, Czech("Vouchery"), null, 0));

        HttpResponseMessage duplicate = await RawSendAsync(client, HttpMethod.Post, "/api/v1/catalog/admin/categories",
            new CreateCategoryRequest("vouchery", null, Czech("Vouchery"), null, 0));
        HttpResponseMessage unknown = await RawSendAsync(client, HttpMethod.Put, $"/api/v1/catalog/admin/products/{Guid.CreateVersion7()}/price",
            new SetPriceRequest(690m, 12m, null));

        duplicate.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Přihlášení jako v prohlížeči: GET vydá antiforgery cookie, POST ji vrátí v hlavičce.
    /// Token je vázaný na uživatele, takže se po přihlášení musí vyzvednout nový.
    /// </summary>
    private static async Task SignInAsync(HttpClient client)
    {
        HttpResponseMessage login = await RawSendAsync(client, HttpMethod.Post, "/api/v1/identity/login", new { Email = AdminEmail, Password = AdminPassword });
        login.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<CatalogAdminDto> SendAsync(HttpClient client, HttpMethod method, string path, object body)
    {
        HttpResponseMessage response = await RawSendAsync(client, method, path, body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await response.Content.ReadFromJsonAsync<CatalogAdminDto>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<HttpResponseMessage> RawSendAsync(HttpClient client, HttpMethod method, string path, object body)
    {
        string token = await XsrfTokenAsync(client);

        using HttpRequestMessage request = new(method, new Uri(path, UriKind.Relative)) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
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

    private static Dictionary<string, string> Czech(string text) => new() { ["cs"] = text };
}
