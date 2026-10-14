using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WakePark.Storefront.Tests;

/// <summary>
/// Jazyky jsou v produkci zatím vypnuté (jen čeština). Tady se zapne angličtina,
/// aby se ověřilo, že směrování, hreflang a texty rozhraní jsou připravené.
/// </summary>
public sealed class LanguageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public LanguageTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _client = factory
            .WithWebHostBuilder(builder => builder
                .UseSetting("Storefront:Languages:0", "cs")
                .UseSetting("Storefront:Languages:1", "en"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Anglicka_verze_je_pod_prefixem_en()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/en/cenik", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        html.ShouldContain("<html lang=\"en\">");
        html.ShouldContain(">Free<");            // texty rozhraní z .resx
        html.ShouldContain("1,200 Kč");      // anglický formát čísel
    }

    [Fact]
    public async Task Ceska_verze_zustava_bez_prefixu()
    {
        string html = await _client.GetStringAsync(new Uri("/cenik", UriKind.Relative), TestContext.Current.CancellationToken);

        html.ShouldContain("<html lang=\"cs\">");
        html.ShouldContain(">Zdarma<");
    }

    [Fact]
    public async Task Obe_verze_na_sebe_odkazuji_pres_hreflang()
    {
        string html = await _client.GetStringAsync(new Uri("/en/kontakt", UriKind.Relative), TestContext.Current.CancellationToken);

        html.ShouldContain("hreflang=\"cs\" href=\"http://localhost/kontakt\"");
        html.ShouldContain("hreflang=\"en\" href=\"http://localhost/en/kontakt\"");
    }

    [Fact]
    public async Task Odkazy_na_anglicke_strance_zustavaji_v_anglictine()
    {
        string html = await _client.GetStringAsync(new Uri("/en", UriKind.Relative), TestContext.Current.CancellationToken);

        html.ShouldContain("href=\"/en/kontakt\"");
        html.ShouldContain("href=\"/en/cenik#vouchery\"");
    }

    [Fact]
    public async Task Nepodporovany_jazyk_je_404()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/de/cenik", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
