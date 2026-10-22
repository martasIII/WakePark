using System.Net;
using System.Text;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Infrastructure.Ares;

namespace WakePark.Modules.Billing.IntegrationTests;

/// <summary>Odpovědi ARES (zkrácené skutečné odpovědi, bez sítě).</summary>
public sealed class AresCompanyRegistryTests
{
    private const string WakePark = """
        {
          "ico": "06633633",
          "obchodniJmeno": "Wake Park s.r.o.",
          "sidlo": {
            "kodStatu": "CZ",
            "nazevObce": "Praha",
            "nazevUlice": "Pavlovská",
            "cisloDomovni": 585,
            "cisloOrientacni": 1,
            "psc": 16200,
            "textovaAdresa": "Pavlovská 585/1, Liboc, 16200 Praha 6"
          },
          "pravniForma": "112",
          "datumVzniku": "2017-10-24",
          "dic": "CZ06633633"
        }
        """;

    private const string NotFound = """
        {"kod":"NENALEZENO","popis":"Nebyl nalezen žádný subjekt, který by odpovídal zadaným hodnotám.","subKod":"VYSTUP_SUBJEKT_NENALEZEN"}
        """;

    [Fact]
    public async Task Firma_se_nacte_s_nazvem_DIC_a_sidlem()
    {
        RegisteredCompany? company = await Registry(new StubAres(HttpStatusCode.OK, WakePark))
            .FindAsync("06633633", TestContext.Current.CancellationToken);

        company.ShouldBe(new RegisteredCompany("06633633", "Wake Park s.r.o.", "CZ06633633", "Pavlovská 585/1, Liboc, 16200 Praha 6", null));
    }

    [Fact]
    public async Task Nepatce_DPH_ma_DIC_prazdne_a_zanikla_firma_datum_zaniku()
    {
        const string body = """{ "ico": "25596641", "obchodniJmeno": "Stará firma s.r.o.", "datumZaniku": "2020-03-31" }""";

        RegisteredCompany? company = await Registry(new StubAres(HttpStatusCode.OK, body))
            .FindAsync("25596641", TestContext.Current.CancellationToken);

        company.ShouldNotBeNull();
        company.VatId.ShouldBeNull();
        company.Address.ShouldBeNull();
        company.DissolvedOn.ShouldBe(new DateOnly(2020, 3, 31));
        company.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Neexistujici_ICO_vrati_null()
    {
        RegisteredCompany? company = await Registry(new StubAres(HttpStatusCode.NotFound, NotFound))
            .FindAsync("06633633", TestContext.Current.CancellationToken);

        company.ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Vypadek_ARES_neni_nenalezeno(HttpStatusCode status)
    {
        await Should.ThrowAsync<CompanyRegistryUnavailableException>(() =>
            Registry(new StubAres(status, "")).FindAsync("06633633", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Druhy_dotaz_na_stejne_ICO_jde_z_pameti()
    {
        StubAres ares = new(HttpStatusCode.OK, WakePark);
        AresCompanyRegistry registry = Registry(ares);

        await registry.FindAsync("06633633", TestContext.Current.CancellationToken);
        await registry.FindAsync("06633633", TestContext.Current.CancellationToken);

        ares.Calls.ShouldBe(1);
        ares.LastPath.ShouldBe("/ekonomicke-subjekty-v-be/rest/ekonomicke-subjekty/06633633");
    }

    /// <summary>Skutečné ARES přes internet. Jen ručně (<c>--explicit only</c>), v CI se nespouští.</summary>
    [Fact(Explicit = true)]
    public async Task Skutecne_ARES_zna_Wake_Park()
    {
        using HttpClient http = new() { BaseAddress = AresCompanyRegistry.BaseAddress, Timeout = TimeSpan.FromSeconds(10) };
        using MemoryCache cache = new(new MemoryCacheOptions());

        RegisteredCompany? company = await new AresCompanyRegistry(http, cache, NullLogger<AresCompanyRegistry>.Instance)
            .FindAsync("06633633", TestContext.Current.CancellationToken);

        company.ShouldNotBeNull();
        company.Name.ShouldBe("Wake Park s.r.o.");
        company.VatId.ShouldBe("CZ06633633");
    }

    private static AresCompanyRegistry Registry(StubAres ares) =>
        new(
            new HttpClient(ares) { BaseAddress = AresCompanyRegistry.BaseAddress },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AresCompanyRegistry>.Instance);

    private sealed class StubAres(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? LastPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastPath = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
