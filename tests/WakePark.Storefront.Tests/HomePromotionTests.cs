using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace WakePark.Storefront.Tests;

/// <summary>Páska s názvem akce nad dlaždicí Wake Shop na úvodu (ADR 0014).</summary>
public sealed class HomePromotionTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CatalogWithPromotions = """
        {
          "categories": [
            { "code": "permanentky", "name": { "cs": "Permanentky" },
              "promotion": { "name": { "cs": "Sezónní akce" }, "discountPercent": 10, "validTo": "2099-12-31" } },
            { "code": "vouchery", "name": { "cs": "Vouchery" },
              "promotion": { "name": { "cs": "Black Friday", "en": "Black Friday EN" }, "discountPercent": 30, "validTo": "2099-12-31" } }
          ],
          "products": []
        }
        """;

    [Fact]
    public async Task Paska_ukaze_akci_z_prvni_kategorie_e_shopu()
    {
        string html = await GetAsync("/", new StubCatalogApi(HttpStatusCode.OK, CatalogWithPromotions));

        // Vouchery jsou v e-shopu první, i když je katalog má až za permanentkami.
        html.ShouldContain("<span class=\"promo-ribbon\"><span>Black Friday</span></span>");
        html.ShouldNotContain("Sezónní akce");
    }

    [Fact]
    public async Task Anglicky_uvod_ukaze_anglicky_nazev() =>
        (await GetAsync("/en", new StubCatalogApi(HttpStatusCode.OK, CatalogWithPromotions)))
            .ShouldContain("<span>Black Friday EN</span>");

    [Fact]
    public async Task Dlouhy_nazev_dostane_mensi_pismo()
    {
        const string catalog = """
            { "categories": [ { "code": "vouchery", "name": { "cs": "Vouchery" },
                "promotion": { "name": { "cs": "Vánoční akce na vouchery" }, "discountPercent": 15, "validTo": "2099-12-31" } } ],
              "products": [] }
            """;

        (await GetAsync("/", new StubCatalogApi(HttpStatusCode.OK, catalog)))
            .ShouldContain("<span class=\"promo-ribbon long\"><span>Vánoční akce na vouchery</span></span>");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{ "categories": [ { "code": "vouchery", "name": { "cs": "Vouchery" } } ], "products": [] }""")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    public async Task Bez_akce_nebo_pri_vypadku_API_je_uvod_bez_pasky(HttpStatusCode status, string body)
    {
        string html = await GetAsync("/", new StubCatalogApi(status, body));

        html.ShouldNotContain("promo-ribbon");
        html.ShouldContain("Wake Shop");
    }

    private async Task<string> GetAsync(string path, StubCatalogApi api)
    {
        HttpResponseMessage response = await factory
            .WithWebHostBuilder(builder => builder.UseCatalogApi(api))
            .CreateClient()
            .GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
