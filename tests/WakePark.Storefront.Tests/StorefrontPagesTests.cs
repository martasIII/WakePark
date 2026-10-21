using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace WakePark.Storefront.Tests;

/// <summary>Web spuštěný v paměti: stránky, přesměrování starých adres, hlavičky.</summary>
public sealed class StorefrontPagesTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
    });

    [Theory]
    [InlineData("/", "Wake Park Džbán")]
    [InlineData("/cenik", "1 200 Kč")]
    [InlineData("/kontakt", "Otevírací doba")]
    public async Task Stranky_se_zobrazi(string path, string expected)
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // Porovnává se text, jak ho uvidí čtenář: Razor zapisuje nezlomitelnou mezeru jako &#xA0;.
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        html.ShouldContain(expected);
        html.ShouldContain("<html lang=\"cs\">");
    }

    [Fact]
    public async Task Uvodni_stranka_ma_osm_dlazdic()
    {
        string html = await _client.GetStringAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        (CountOccurrences(html, "class=\"tile\"") + CountOccurrences(html, "class=\"tile soon\"")).ShouldBe(8);
        html.ShouldContain("href=\"/eshop\"");
    }

    [Theory]
    [InlineData("/cenik.html", "/cenik")]
    [InlineData("/kontakt/", "/kontakt")]
    [InlineData("/index.html", "/")]
    [InlineData("/novy/cenik.html", "/cenik.html")]
    [InlineData("/shop/", "/cenik#vouchery")]
    public async Task Stare_adresy_se_trvale_presmeruji(string oldPath, string newPath)
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri(oldPath, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.OriginalString.ShouldEndWith(newPath);
    }

    [Fact]
    public async Task Rezervace_vedou_do_rezervacniho_systemu()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/rezervace", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.Host.ShouldBe("r2.wakearealy.cz");
    }

    [Fact]
    public async Task Neexistujici_stranka_vrati_404_s_odkazy_dal()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/o-parku-old/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.ShouldContain("href=\"/cenik\"");
    }

    [Fact]
    public async Task Odpoved_nese_bezpecnostni_hlavicky()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("frame-ancestors 'none'");
        response.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
    }

    [Fact]
    public async Task Health_odpovida()
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static int CountOccurrences(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
