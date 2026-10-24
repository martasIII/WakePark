using System.Net;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using WakePark.Storefront.Eshop;

namespace WakePark.Storefront.Tests;

/// <summary>Katalog API nahrazený pevnou odpovědí — ceník a e-shop web čtou z API (ADR 0009).</summary>
internal static class TestCatalogApi
{
    /// <summary>Ceník, jak ho vrací <c>GET …/price-list</c>: sekce, podsekce, položka zdarma, sekce jen s popisem.</summary>
    public const string PriceListJson = """
        {
          "title": { "cs": "Sezóna 2025", "en": "Season 2025" },
          "currency": "CZK",
          "sections": [
            {
              "code": "jizdne", "name": { "cs": "Jízdné", "en": "Rides" }, "description": null,
              "items": [
                { "code": "jizda-60-cely-vlek", "name": { "cs": "60 minut (celý vlek)", "en": "60 minutes (whole cable)" },
                  "description": { "cs": "Pro max. 4 osoby", "en": "For up to 4 people" }, "badge": null, "unitPrice": 1200 }
              ],
              "subsections": []
            },
            {
              "code": "pujcovna", "name": { "cs": "Půjčovna", "en": "Equipment rental" },
              "description": { "cs": "Po dobu jízdného.", "en": "For the duration of your ride." },
              "items": [
                { "code": "helma", "name": { "cs": "Helma", "en": "Helmet" }, "description": null, "badge": null, "unitPrice": 0 }
              ],
              "subsections": [
                {
                  "code": "prkna", "name": { "cs": "Prkna", "en": "Boards" }, "description": null,
                  "items": [
                    { "code": "wakeboard", "name": { "cs": "Wakeboard" }, "description": null,
                      "badge": { "cs": "Novinka", "en": "New" }, "unitPrice": 150 }
                  ],
                  "subsections": []
                }
              ]
            },
            { "code": "vouchery", "name": { "cs": "Dárkové vouchery", "en": "Gift vouchers" }, "description": null,
              "items": [ { "code": "voucher-1-slot", "name": { "cs": "Voucher pro jednoho" }, "description": null, "badge": null, "unitPrice": 690 } ],
              "subsections": [] },
            {
              "code": "firemni-akce", "name": { "cs": "Firemní akce", "en": "Corporate events" },
              "description": { "cs": "Napište nám na dan@wake-dzban.cz.", "en": "Write to dan@wake-dzban.cz." },
              "items": [], "subsections": []
            }
          ]
        }
        """;

    /// <summary>Web napojený na falešné API katalogu.</summary>
    public static IWebHostBuilder UseCatalogApi(this IWebHostBuilder builder, HttpMessageHandler api) =>
        builder
            .UseSetting("Storefront:ApiBaseUrl", "http://api.test")
            .UseSetting("Storefront:EnterpriseId", "01990000-0000-7000-8000-000000000001")
            .ConfigureTestServices(services =>
                services.AddHttpClient<CatalogClient>().ConfigurePrimaryHttpMessageHandler(() => api));
}

/// <summary>Místo API — vrací pevnou odpověď; dá se přepnout na výpadek.</summary>
internal sealed class StubCatalogApi(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = status;

    public int Calls { get; private set; }

    public static StubCatalogApi WithPriceList() => new(HttpStatusCode.OK, TestCatalogApi.PriceListJson);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
