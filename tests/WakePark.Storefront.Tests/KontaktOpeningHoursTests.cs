using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using WakePark.Storefront.Content;

namespace WakePark.Storefront.Tests;

/// <summary>
/// Otevírací doba na stránce Kontakt z provozních dní v API (ADR 0009). Zapíná se přepínačem
/// až po nahrání CSV sezóny; do té doby web čte <c>oteviraci-doba.json</c>.
/// </summary>
public sealed class KontaktOpeningHoursTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string HoursJson = """
        {
          "timeZoneId": "Europe/Prague",
          "days": [
            { "date": "2027-05-01", "opensAt": "10:00:00", "closesAt": "19:30:00",
              "blockings": [
                { "cableName": "Vlek 1", "from": "10:00:00", "to": "12:00:00", "category": "PrivateEvent" },
                { "cableName": null, "from": "17:00:00", "to": "18:00:00", "category": "Weather" }
              ] },
            { "date": "2027-05-02", "opensAt": null, "closesAt": null, "blockings": [] }
          ]
        }
        """;

    [Fact]
    public async Task S_prepinacem_ukaze_dny_z_API_zavreno_a_blokace_s_duvodem()
    {
        string html = await GetAsync(new StubCatalogApi(HttpStatusCode.OK, HoursJson), fromApi: true);

        html.ShouldContain("10:00–19:30");
        html.ShouldContain("Zavřeno");
        html.ShouldContain("Vlek 1 10:00–12:00: soukromá akce");
        html.ShouldContain("Celý areál 17:00–18:00: počasí");
        html.ShouldContain("1. 5.");
    }

    [Fact]
    public async Task Bez_prepinace_cte_web_dal_soubor()
    {
        StubCatalogApi api = new(HttpStatusCode.OK, HoursJson);

        string html = await GetAsync(api, fromApi: false);

        api.Calls.ShouldBe(0);
        html.ShouldContain("Pondělí");
    }

    [Fact]
    public async Task Kdyz_API_neodpovida_ukaze_telefon()
    {
        string html = await GetAsync(new StubCatalogApi(HttpStatusCode.ServiceUnavailable, ""), fromApi: true);

        html.ShouldContain("Údaje se teď nepodařilo načíst");
    }

    private async Task<string> GetAsync(StubCatalogApi api, bool fromApi)
    {
        using HttpClient client = factory
            .WithWebHostBuilder(builder => builder
                .UseSetting("Storefront:ApiBaseUrl", "http://api.test")
                .UseSetting("Storefront:WakeParkId", "01a1a2d0-5f3e-7c41-9b8e-2a6d0c7e4f15")
                .UseSetting("Storefront:OpeningHoursFromApi", fromApi ? "true" : "false")
                .ConfigureTestServices(services =>
                    services.AddHttpClient<OpeningHoursClient>().ConfigurePrimaryHttpMessageHandler(() => api)))
            .CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/kontakt", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
