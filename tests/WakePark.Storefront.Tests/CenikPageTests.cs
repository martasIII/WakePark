using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace WakePark.Storefront.Tests;

/// <summary>Ceník z katalogu v API (ADR 0009): sekce, podsekce, zdarma, štítek, výpadek API.</summary>
public sealed class CenikPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Cenik_ukaze_sekce_podsekce_ceny_a_poznamky()
    {
        string html = await GetAsync(CreateFactory(StubCatalogApi.WithPriceList()), "/cenik");

        html.ShouldContain("Sezóna 2025");
        html.ShouldContain("id=\"pujcovna\"");
        html.ShouldContain("<h3 class=\"subsection\" id=\"prkna\">Prkna</h3>");
        html.ShouldContain("1 200 Kč");
        html.ShouldContain(">Zdarma<");
        html.ShouldContain("<span class=\"tag\">Novinka</span>");
        html.ShouldContain("Po dobu jízdného.");
        html.ShouldContain("href=\"mailto:dan@wake-dzban.cz\"");
    }

    [Fact]
    public async Task Sekce_vouchery_na_kterou_vedou_stare_odkazy_existuje()
    {
        string html = await GetAsync(CreateFactory(StubCatalogApi.WithPriceList()), "/cenik");

        html.ShouldContain("id=\"vouchery\"");
    }

    [Fact]
    public async Task Pri_vypadku_API_ukaze_posledni_nacteny_cenik()
    {
        StubCatalogApi api = StubCatalogApi.WithPriceList();
        WebApplicationFactory<Program> app = CreateFactory(api);
        await GetAsync(app, "/cenik");

        // Čerstvá kopie vyprší, API mezitím spadne.
        app.Services.GetRequiredService<IMemoryCache>().Remove("price-list");
        api.Status = HttpStatusCode.ServiceUnavailable;

        string html = await GetAsync(app, "/cenik");

        html.ShouldContain("1 200 Kč");
        api.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Prazdny_cenik_ukaze_telefon()
    {
        string html = await GetAsync(
            CreateFactory(new StubCatalogApi(HttpStatusCode.OK, """{ "title": null, "currency": "CZK", "sections": [] }""")),
            "/cenik");

        html.ShouldContain("Údaje se teď nepodařilo načíst");
    }

    private WebApplicationFactory<Program> CreateFactory(StubCatalogApi api) =>
        factory.WithWebHostBuilder(builder => builder.UseCatalogApi(api));

    private static async Task<string> GetAsync(WebApplicationFactory<Program> app, string path)
    {
        using HttpClient client = app.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
