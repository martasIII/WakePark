using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using WakePark.Storefront.Eshop;

namespace WakePark.Storefront.Tests;

/// <summary>Objednávka z pohledu zákazníka: formulář → API (nahrazené v testu) → stránka s QR.</summary>
public sealed partial class OrderFlowTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ProductId = "01990000-0000-7000-8000-0000000000a1";
    private const string Token = "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";

    private const string CatalogJson = $$"""
        { "categories": [ { "code": "vouchery", "name": { "cs": "Vouchery" } } ],
          "products": [ { "id": "{{ProductId}}", "code": "voucher-2-sloty", "categoryCode": "vouchery",
            "name": { "cs": "Voucher pro dva" }, "description": { "cs": "2 × 20 minut" }, "imagePath": null,
            "unitPrice": 1290, "currency": "CZK", "vatRate": 12 } ] }
        """;

    private const string OrderJson = """
        { "number": 261000, "status": "AwaitingPayment", "language": "cs",
          "customerName": "Jana Nováková", "customerEmail": "jana@example.cz", "companyName": null,
          "lines": [ { "productCode": "voucher-2-sloty", "name": "Voucher pro dva", "quantity": 2,
                       "unitPrice": 1290, "vatRate": 12, "lineGross": 2580 } ],
          "totalGross": 2580, "totalVat": 276.43, "totalNet": 2303.57, "currency": "CZK",
          "placedAtUtc": "2026-10-21T18:00:00Z", "paymentDueAtUtc": "2026-10-25T18:00:00Z",
          "payment": { "accountNumber": "2601338268/2010", "iban": "CZ9520100000002601338268", "bic": "FIOBCZPPXXX",
                       "variableSymbol": "261000", "amount": 2580, "currency": "CZK", "message": "Wake Park objednavka 261000",
                       "spayd": "SPD*1.0*ACC:CZ9520100000002601338268*AM:2580.00*CC:CZK*X-VS:261000" } }
        """;

    [Fact]
    public async Task Odeslana_objednavka_presmeruje_na_stranku_objednavky()
    {
        FakeApi api = new() { PlaceResponse = (HttpStatusCode.Created, $$"""{ "number": 261000, "accessToken": "{{Token}}" }""") };
        HttpClient client = CreateClient(api);

        HttpResponseMessage response = await PostOrderAsync(client, quantity: 2);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe($"/objednavka/{Token}");

        string sent = api.PlacedBodies.Single();
        sent.ShouldContain($"\"productId\":\"{ProductId}\"");
        sent.ShouldContain("\"quantity\":2");
        sent.ShouldContain("\"termsAccepted\":true");
        sent.ShouldNotContain("1290", Case.Sensitive, "cenu web neposílá, určuje ji server");
    }

    [Fact]
    public async Task Chyby_z_API_se_zakaznikovi_vypisou_a_vyplnene_udaje_zustanou()
    {
        FakeApi api = new()
        {
            PlaceResponse = (HttpStatusCode.UnprocessableEntity,
                """{ "status": 422, "detail": "IČO nemá platný tvar (8 číslic s kontrolní číslicí)." }"""),
        };

        HttpResponseMessage response = await PostOrderAsync(CreateClient(api), quantity: 2);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        html.ShouldContain("IČO nemá platný tvar");
        html.ShouldContain("value=\"jana@example.cz\"");
        html.ShouldContain("value=\"2\"");
    }

    [Fact]
    public async Task Bez_vybraneho_produktu_se_API_vubec_nevola()
    {
        FakeApi api = new();

        HttpResponseMessage response = await PostOrderAsync(CreateClient(api), quantity: 0);

        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        html.ShouldContain("Vyberte aspoň jeden produkt.");
        api.PlacedBodies.ShouldBeEmpty();
    }

    [Fact]
    public async Task Vyplnene_skryte_pole_objednavku_tise_zahodi()
    {
        FakeApi api = new();

        HttpResponseMessage response = await PostOrderAsync(CreateClient(api), quantity: 1, honeypot: "http://spam.example");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/eshop");
        api.PlacedBodies.ShouldBeEmpty();
    }

    [Fact]
    public async Task Stranka_objednavky_ukaze_QR_a_platebni_udaje_a_neindexuje_se()
    {
        FakeApi api = new();

        HttpResponseMessage response = await CreateClient(api)
            .GetAsync(new Uri($"/objednavka/{Token}", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        html.ShouldContain("<svg");
        html.ShouldContain("2601338268/2010");
        html.ShouldContain("261000");
        // 25. 10. 2026 končí letní čas: 18:00 UTC je v Praze 19:00 (UTC+1), ne 20:00.
        html.ShouldContain("25. 10. 2026 19:00");
        html.ShouldContain("<meta name=\"robots\" content=\"noindex\">");
    }

    [Fact]
    public async Task Neznamy_token_je_404()
    {
        HttpResponseMessage response = await CreateClient(new FakeApi())
            .GetAsync(new Uri("/objednavka/neexistuje", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Prilis_mnoho_objednavek_z_jedne_IP_se_odmitne()
    {
        FakeApi api = new() { PlaceResponse = (HttpStatusCode.Created, $$"""{ "number": 261000, "accessToken": "{{Token}}" }""") };
        HttpClient client = CreateClient(api);

        HttpStatusCode last = HttpStatusCode.OK;
        for (int i = 0; i < 6; i++)
        {
            last = (await PostOrderAsync(client, quantity: 1)).StatusCode;
        }

        last.ShouldBe(HttpStatusCode.TooManyRequests);
        api.PlacedBodies.Count.ShouldBe(5);
    }

    private static async Task<HttpResponseMessage> PostOrderAsync(HttpClient client, int quantity, string honeypot = "")
    {
        // Formulář nese antiforgery token — vezme se z vykreslené stránky, jako v prohlížeči.
        string page = await client.GetStringAsync(new Uri("/eshop", UriKind.Relative), TestContext.Current.CancellationToken);
        string antiforgery = AntiforgeryToken().Match(page).Groups[1].Value;

        using FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            [$"Quantities[{ProductId}]"] = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Form.Name"] = "Jana Nováková",
            ["Form.Email"] = "jana@example.cz",
            ["Form.TermsAccepted"] = "true",
            ["Form.Website"] = honeypot,
            ["__RequestVerificationToken"] = antiforgery,
        });

        return await client.PostAsync(new Uri("/eshop", UriKind.Relative), form, TestContext.Current.CancellationToken);
    }

    private HttpClient CreateClient(FakeApi api) =>
        factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Storefront:ApiBaseUrl", "http://api.test");
                builder.UseSetting("Storefront:EnterpriseId", "01990000-0000-7000-8000-000000000001");
                builder.ConfigureTestServices(services =>
                {
                    services.AddHttpClient<CatalogClient>().ConfigurePrimaryHttpMessageHandler(() => api);
                    services.AddHttpClient<OrderClient>().ConfigurePrimaryHttpMessageHandler(() => api);
                });
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();

    /// <summary>Místo API: nabídka, založení objednávky a objednávka podle tokenu.</summary>
    private sealed class FakeApi : HttpMessageHandler
    {
        public (HttpStatusCode Status, string Body) PlaceResponse { get; init; } = (HttpStatusCode.InternalServerError, "");

        public List<string> PlacedBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;

            (HttpStatusCode status, string body) = path switch
            {
                _ when path.StartsWith("/api/v1/catalog/", StringComparison.Ordinal) => (HttpStatusCode.OK, CatalogJson),
                _ when request.Method == HttpMethod.Post => PlaceResponse,
                _ when path == $"/api/v1/billing/orders/{Token}" => (HttpStatusCode.OK, OrderJson),
                _ => (HttpStatusCode.NotFound, ""),
            };

            if (request.Method == HttpMethod.Post)
            {
                PlacedBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            }

            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
