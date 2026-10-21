using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Shouldly;

using Testcontainers.PostgreSql;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Infrastructure.Persistence;
using WakePark.Modules.Catalog.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Catalog.IntegrationTests;

/// <summary>
/// Import a čtení nabídky proti skutečnému PostgreSQL: vícejazyčné texty v <c>jsonb</c>,
/// dotaz pro web a to, že opakovaný import nevyrábí zbytečné UPDATE.
/// Každý test dostane vlastní kontext — jako dva oddělené běhy importu.
/// </summary>
public sealed class CatalogPersistenceTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    public async ValueTask InitializeAsync()
    {
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

        CatalogDto catalog = await ReadAsync();

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
    public async Task Skryty_produkt_ani_prazdna_kategorie_se_nenabizi()
    {
        await ImportAsync(Catalog(
            Product("voucher-1-slot", "vouchery", 690m),
            Product("permanentka-10x20", "permanentky", 4200m)));

        CatalogImportResult result = await ImportAsync(Catalog(Product("voucher-1-slot", "vouchery", 690m)));

        result.Deactivated.ShouldBe(1);
        CatalogDto catalog = await ReadAsync();
        catalog.Products.Select(p => p.Code).ShouldBe(["voucher-1-slot"]);
        catalog.Categories.Select(c => c.Code).ShouldBe(["vouchery"]);
    }

    [Fact]
    public async Task Opakovany_import_v_novem_kontextu_nic_nezmeni()
    {
        CatalogDefinition definition = Catalog(Product("voucher-1-slot", "vouchery", 690m));
        await ImportAsync(definition);

        CatalogImportResult result = await ImportAsync(definition);

        result.ShouldBe(new CatalogImportResult(Created: 0, Updated: 0, Deactivated: 0, Unchanged: 1));

        // Kdyby converter jsonb neporovnával texty podle hodnoty, EF by řádek považoval za změněný.
        await using CatalogDbContext dbContext = CreateDbContext();
        var product = await dbContext.Products.SingleAsync(TestContext.Current.CancellationToken);
        product.UpdatedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Neznamy_provozovatel_ma_prazdnou_nabidku()
    {
        await ImportAsync(Catalog(Product("voucher-1-slot", "vouchery", 690m)));

        await using CatalogDbContext dbContext = CreateDbContext();
        CatalogDto catalog = await new CatalogReadRepository(dbContext)
            .GetPublishedCatalogAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        catalog.Products.ShouldBeEmpty();
        catalog.Categories.ShouldBeEmpty();
    }

    private async Task<CatalogImportResult> ImportAsync(CatalogDefinition definition)
    {
        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));

        await using CatalogDbContext dbContext = CreateDbContext(clock);
        return await new ImportCatalogCommandHandler(new CatalogRepository(dbContext), dbContext, clock)
            .HandleAsync(new ImportCatalogCommand(EnterpriseId, definition), TestContext.Current.CancellationToken);
    }

    private async Task<CatalogDto> ReadAsync()
    {
        await using CatalogDbContext dbContext = CreateDbContext();
        return await new CatalogReadRepository(dbContext)
            .GetPublishedCatalogAsync(EnterpriseId, TestContext.Current.CancellationToken);
    }

    private CatalogDbContext CreateDbContext(IClock? clock = null)
    {
        DbContextOptionsBuilder<CatalogDbContext> builder = new DbContextOptionsBuilder<CatalogDbContext>();
        builder.UseWakeParkNpgsql(_container.GetConnectionString(), CatalogDbContext.Schema);

        if (clock is not null)
        {
            builder.AddInterceptors(new AuditInterceptor(clock, Substitute.For<ICurrentUser>()));
        }

        return new CatalogDbContext(builder.Options);
    }

    private static CatalogDefinition Catalog(params ProductDefinition[] products) =>
        new(
            "CZK",
            [new CategoryDefinition("vouchery", Text("vouchery")), new CategoryDefinition("permanentky", Text("permanentky"))],
            products);

    private static ProductDefinition Product(string code, string category, decimal price) =>
        new(code, category, Text(code), Text("Popis"), "/img/katalog/voucher-1.svg", price, 21m);

    private static Dictionary<string, string> Text(string czech) => new() { ["cs"] = czech, ["en"] = czech + " EN" };
}
