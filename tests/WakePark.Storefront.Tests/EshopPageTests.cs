using System.Net;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using WakePark.Storefront.Eshop;

namespace WakePark.Storefront.Tests;

/// <summary>
/// Stránka e-shopu s API nahrazeným odpovědí z testu. Hlavně: když API neodpovídá,
/// stránka se nerozbije a ukáže telefon.
/// </summary>
public sealed class EshopPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CatalogJson = """
        {
          "categories": [ { "code": "vouchery", "name": { "cs": "Vouchery", "en": "Vouchers" } } ],
          "products": [
            {
              "id": "01990000-0000-7000-8000-0000000000a1",
              "code": "voucher-2-sloty",
              "categoryCode": "vouchery",
              "name": { "cs": "Voucher pro dva", "en": "Voucher for two" },
              "description": { "cs": "2 × 20 minut" },
              "imagePath": "/img/katalog/voucher-2.svg",
              "unitPrice": 1290,
              "currency": "CZK",
              "vatRate": 21
            }
          ]
        }
        """;

    [Fact]
    public async Task Nabidka_z_API_se_zobrazi_s_cenou()
    {
        StubHandler api = new(HttpStatusCode.OK, CatalogJson);

        string html = await GetEshopAsync(api);

        html.ShouldContain("Voucher pro dva");
        // Cena se píše s nezlomitelnými mezerami (tisíce i před „Kč“), aby se nezalomila.
        html.ShouldContain("1 290 Kč");
        html.ShouldContain("src=\"/img/katalog/voucher-2.svg\"");
        api.RequestedUris.Single().AbsolutePath
            .ShouldBe("/api/v1/catalog/enterprises/01990000-0000-7000-8000-000000000001");
    }

    [Fact]
    public async Task Nabidka_se_drzi_v_pameti_a_API_se_pri_kazdem_nacteni_nepta()
    {
        StubHandler api = new(HttpStatusCode.OK, CatalogJson);
        HttpClient client = CreateClient(api);

        await client.GetStringAsync(new Uri("/eshop", UriKind.Relative), TestContext.Current.CancellationToken);
        await client.GetStringAsync(new Uri("/eshop", UriKind.Relative), TestContext.Current.CancellationToken);

        api.RequestedUris.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.OK, "tohle není JSON")]
    public async Task Kdyz_API_selze_ukaze_se_telefon(HttpStatusCode status, string body)
    {
        string html = await GetEshopAsync(new StubHandler(status, body));

        html.ShouldContain("tel:+420737562516");
        html.ShouldNotContain("class=\"product\"");
    }

    [Fact]
    public async Task Bez_nastaveni_API_se_stranka_nerozbije()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/eshop", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("tel:+420737562516");
    }

    private async Task<string> GetEshopAsync(StubHandler api)
    {
        HttpResponseMessage response = await CreateClient(api)
            .GetAsync(new Uri("/eshop", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private HttpClient CreateClient(StubHandler api) =>
        factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Storefront:ApiBaseUrl", "http://api.test");
                builder.UseSetting("Storefront:EnterpriseId", "01990000-0000-7000-8000-000000000001");
                builder.ConfigureTestServices(services =>
                    services.AddHttpClient<CatalogClient>().ConfigurePrimaryHttpMessageHandler(() => api));
            })
            .CreateClient();

    /// <summary>Místo API — vrací pevnou odpověď a pamatuje si, na co se web ptal.</summary>
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
