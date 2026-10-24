using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Shouldly;

using Testcontainers.PostgreSql;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Catalog.Application;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Application.Catalog;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Domain.Products;
using WakePark.Modules.Catalog.Infrastructure.Persistence;
using WakePark.Modules.Catalog.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Catalog.IntegrationTests;

/// <summary>
/// Import a čtení katalogu proti skutečnému PostgreSQL: vícejazyčné texty v <c>jsonb</c>
/// (i nepovinné), strom kategorií, místa zobrazení, ceny s platností a to, že opakovaný import
/// nevyrábí zbytečné UPDATE. Každý krok dostane vlastní kontext — jako oddělené požadavky.
/// </summary>
public sealed class CatalogPersistenceTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    private readonly IClock _clock = Substitute.For<IClock>();

    public async ValueTask InitializeAsync()
    {
        _clock.UtcNow.Returns(Now);
        await _container.StartAsync();

        await using CatalogDbContext dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task Naimportovany_katalog_se_precte_serazeny_i_s_preklady()
    {
        await ImportAsync(Catalog(
            Product("voucher-1-slot", "vouchery", 690m),
            Product("permanentka-10x20", "permanentky", 4200m),
            Product("voucher-2-sloty", "vouchery", 1290m)));

        CatalogDto catalog = await EshopAsync();

        catalog.Categories.Select(c => c.Code).ShouldBe(["vouchery", "permanentky"]);
        catalog.Categories[0].Name["en"].ShouldBe("vouchery EN");
        catalog.Products.Select(p => p.Code).ShouldBe(["voucher-1-slot", "voucher-2-sloty", "permanentka-10x20"]);

        CatalogProductDto voucher = catalog.Products[0];
        voucher.Name.ShouldBe(new Dictionary<string, string> { ["cs"] = "voucher-1-slot", ["en"] = "voucher-1-slot EN" });
        voucher.UnitPrice.ShouldBe(690m);
        voucher.VatRate.ShouldBe(21m);
        voucher.Currency.ShouldBe("CZK");
    }

    [Fact]
    public async Task Cenik_s_podsekci_nepovinnym_popisem_a_polozkou_jen_pro_cenik()
    {
        CatalogDefinition definition = Catalog(
            Product("voucher-1-slot", "vouchery", 690m),
            Product("helma", "pujcovna", 0m) with { Description = null, ShowIn = ["pricelist", "pos"] }) with
        {
            Title = Text("Sezóna 2027"),
            Categories =
            [
                new CategoryDefinition("vouchery", Text("vouchery")),
                new CategoryDefinition("permanentky", Text("permanentky")),
                new CategoryDefinition("pujcovna", Text("pujcovna"), Text("Po dobu jízdného."), Parent: "permanentky"),
            ],
        };

        await ImportAsync(definition);

        PublicPriceListDto priceList = await PriceListAsync();
        priceList.Title!["cs"].ShouldBe("Sezóna 2027");
        priceList.Sections.Select(s => s.Code).ShouldBe(["vouchery", "permanentky"]);
        PriceListSectionDto pujcovna = priceList.Sections[1].Subsections.Single();
        pujcovna.Description!["cs"].ShouldBe("Po dobu jízdného.");
        pujcovna.Items.Single().Description.ShouldBeNull();
        pujcovna.Items.Single().UnitPrice.ShouldBe(0m);

        (await EshopAsync()).Products.Select(p => p.Code).ShouldBe(["voucher-1-slot"]);

        await using CatalogDbContext dbContext = CreateDbContext();
        Product helma = await dbContext.Products.SingleAsync(p => p.Code == "helma", TestContext.Current.CancellationToken);
        helma.Channels.ShouldBe(ProductChannels.PriceList | ProductChannels.Pos);
    }

    [Fact]
    public async Task Prepis_skryje_produkt_a_prazdna_kategorie_se_nenabizi()
    {
        await ImportAsync(Catalog(
            Product("voucher-1-slot", "vouchery", 690m),
            Product("permanentka-10x20", "permanentky", 4200m)));

        CatalogImportResult result = await ImportAsync(Catalog(Product("voucher-1-slot", "vouchery", 690m)), CatalogImportMode.Overwrite);

        result.Deactivated.ShouldBe(1);
        CatalogDto catalog = await EshopAsync();
        catalog.Products.Select(p => p.Code).ShouldBe(["voucher-1-slot"]);
        catalog.Categories.Select(c => c.Code).ShouldBe(["vouchery"]);
    }

    [Fact]
    public async Task Opakovany_prepis_v_novem_kontextu_nic_nezmeni()
    {
        CatalogDefinition definition = Catalog(Product("voucher-1-slot", "vouchery", 690m));
        await ImportAsync(definition);

        CatalogImportResult result = await ImportAsync(definition, CatalogImportMode.Overwrite);

        result.ShouldBe(new CatalogImportResult(CatalogImportMode.Overwrite, Created: 0, Updated: 0, Deactivated: 0, Unchanged: 1));

        // Kdyby converter jsonb neporovnával texty podle hodnoty, EF by řádek považoval za změněný.
        await using CatalogDbContext dbContext = CreateDbContext();
        Product product = await dbContext.Products.SingleAsync(TestContext.Current.CancellationToken);
        product.UpdatedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Ceny_s_ruznou_platnosti_se_ulozi_vedle_sebe()
    {
        await ImportAsync(Catalog(Product("voucher-1-slot", "vouchery", 690m)));
        _clock.UtcNow.Returns(Now.AddDays(1));

        await ImportAsync(Catalog(Product("voucher-1-slot", "vouchery", 750m)), CatalogImportMode.Overwrite);

        await using CatalogDbContext dbContext = CreateDbContext();
        (await dbContext.PriceItems.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
        (await EshopAsync()).Products.Single().UnitPrice.ShouldBe(750m);
    }

    [Fact]
    public async Task Neznamy_provozovatel_ma_prazdnou_nabidku()
    {
        await ImportAsync(Catalog(Product("voucher-1-slot", "vouchery", 690m)));

        CatalogDto catalog = await EshopAsync(Guid.CreateVersion7());

        catalog.Products.ShouldBeEmpty();
        catalog.Categories.ShouldBeEmpty();
    }

    private async Task<CatalogImportResult> ImportAsync(CatalogDefinition definition, CatalogImportMode mode = CatalogImportMode.AddMissing)
    {
        await using CatalogDbContext dbContext = CreateDbContext(withAudit: true);
        return await new ImportCatalogCommandHandler(new CatalogRepository(dbContext), dbContext, Calendar())
            .HandleAsync(new ImportCatalogCommand(EnterpriseId, definition, mode), TestContext.Current.CancellationToken);
    }

    private async Task<CatalogDto> EshopAsync(Guid? enterpriseId = null)
    {
        await using CatalogDbContext dbContext = CreateDbContext();
        return await new CatalogReader(new CatalogReadRepository(dbContext), Calendar())
            .GetPublishedCatalogAsync(enterpriseId ?? EnterpriseId, TestContext.Current.CancellationToken);
    }

    private async Task<PublicPriceListDto> PriceListAsync()
    {
        await using CatalogDbContext dbContext = CreateDbContext();
        return await new GetPublicPriceListQueryHandler(new CatalogReadRepository(dbContext), Calendar())
            .HandleAsync(new GetPublicPriceListQuery(EnterpriseId), TestContext.Current.CancellationToken);
    }

    private CatalogCalendar Calendar() => new(_clock, new CatalogOptions());

    private CatalogDbContext CreateDbContext(bool withAudit = false)
    {
        DbContextOptionsBuilder<CatalogDbContext> builder = new DbContextOptionsBuilder<CatalogDbContext>();
        builder.UseWakeParkNpgsql(_container.GetConnectionString(), CatalogDbContext.Schema);

        if (withAudit)
        {
            builder.AddInterceptors(new AuditInterceptor(_clock, Substitute.For<ICurrentUser>()));
        }

        return new CatalogDbContext(builder.Options);
    }

    private static CatalogDefinition Catalog(params ProductDefinition[] products) =>
        new(
            "CZK",
            null,
            [new CategoryDefinition("vouchery", Text("vouchery")), new CategoryDefinition("permanentky", Text("permanentky"))],
            products);

    private static ProductDefinition Product(string code, string category, decimal price) =>
        new(code, category, Text(code), Text("Popis"), "/img/katalog/voucher-1.svg", price, 21m, ["pricelist", "eshop", "pos"]);

    private static Dictionary<string, string> Text(string czech) => new() { ["cs"] = czech, ["en"] = czech + " EN" };
}
