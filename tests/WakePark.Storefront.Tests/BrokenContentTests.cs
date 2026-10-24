using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WakePark.Storefront.Tests;

/// <summary>
/// Co uvidí návštěvník, když je obsah na serveru špatně nebo API neodpovídá: stránka se
/// nesmí rozbít, místo ceníku a otevírací doby ukáže telefon.
/// </summary>
public sealed class BrokenContentTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dataDirectory = Directory.CreateTempSubdirectory("wakepark-storefront-").FullName;
    private readonly HttpClient _client;

    public BrokenContentTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        // Syntakticky platný JSON, který ale nedává smysl: chybí neděle.
        File.WriteAllText(Path.Combine(_dataDirectory, "oteviraci-doba.json"),
            """{ "week": { "mon": "10–20", "tue": "10–20", "wed": "10–20", "thu": "10–20", "fri": "10–20", "sat": "10–20" } }""");

        _client = factory
            .WithWebHostBuilder(builder => builder
                .UseSetting("Storefront:ContentPath", _dataDirectory)
                .UseCatalogApi(new StubCatalogApi(HttpStatusCode.InternalServerError, "")))
            .CreateClient();
    }

    [Theory]
    [InlineData("/cenik", "60 minut (celý vlek)")]
    [InlineData("/kontakt", "Pondělí")]
    public async Task Stranka_misto_obsahu_ukaze_telefon(string path, string contentThatMustNotAppear)
    {
        HttpResponseMessage response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        html.ShouldContain("Údaje se teď nepodařilo načíst");
        html.ShouldNotContain(contentThatMustNotAppear);
    }

    public void Dispose()
    {
        _client.Dispose();
        Directory.Delete(_dataDirectory, recursive: true);
    }
}
