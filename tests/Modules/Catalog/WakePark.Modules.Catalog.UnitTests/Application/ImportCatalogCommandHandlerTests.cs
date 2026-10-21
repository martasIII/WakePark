using FluentValidation;

using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.UnitTests.Application;

public sealed class ImportCatalogCommandHandlerTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();

    private readonly InMemoryCatalogRepository _repository = new();
    private readonly ICatalogUnitOfWork _unitOfWork = Substitute.For<ICatalogUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public ImportCatalogCommandHandlerTests()
    {
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Prvni_import_zalozi_kategorie_produkty_a_ceny()
    {
        CatalogImportResult result = await ImportAsync(Catalog(Voucher("voucher-1-slot", 690m), Voucher("voucher-2-sloty", 1290m)));

        result.ShouldBe(new CatalogImportResult(Created: 2, Updated: 0, Deactivated: 0, Unchanged: 0));
        _repository.Categories.Count.ShouldBe(1);
        _repository.PriceLists.Single().Items.Count.ShouldBe(2);
        _repository.Products.Select(p => p.SortOrder).ShouldBe([0, 1]);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Opakovany_import_stejneho_souboru_nic_nezmeni()
    {
        CatalogDefinition catalog = Catalog(Voucher("voucher-1-slot", 690m));
        await ImportAsync(catalog);

        CatalogImportResult result = await ImportAsync(catalog);

        result.ShouldBe(new CatalogImportResult(Created: 0, Updated: 0, Deactivated: 0, Unchanged: 1));
        _repository.Products.Count.ShouldBe(1);
        _repository.PriceLists.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Zmena_ceny_je_zmena_produktu()
    {
        await ImportAsync(Catalog(Voucher("voucher-1-slot", 690m)));

        CatalogImportResult result = await ImportAsync(Catalog(Voucher("voucher-1-slot", 750m)));

        result.Updated.ShouldBe(1);
        _repository.PriceLists.Single().Items.Single().UnitPrice.ShouldBe(750m);
    }

    [Fact]
    public async Task Produkt_ktery_ze_souboru_zmizi_se_skryje_a_po_navratu_zase_zobrazi()
    {
        await ImportAsync(Catalog(Voucher("voucher-1-slot", 690m), Voucher("voucher-2-sloty", 1290m)));

        CatalogImportResult removed = await ImportAsync(Catalog(Voucher("voucher-1-slot", 690m)));

        removed.Deactivated.ShouldBe(1);
        _repository.Products.Count.ShouldBe(2, "produkt se nikdy nemaže, odkazují na něj prodané poukazy");
        _repository.Products.Single(p => p.Code == "voucher-2-sloty").IsActive.ShouldBeFalse();

        CatalogImportResult returned = await ImportAsync(Catalog(Voucher("voucher-1-slot", 690m), Voucher("voucher-2-sloty", 1290m)));

        returned.Updated.ShouldBe(1);
        _repository.Products.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public async Task Neplatny_soubor_neulozi_nic()
    {
        CatalogDefinition catalog = Catalog(Voucher("voucher-1-slot", 690m), Voucher("voucher-1-slot", 0m));

        ValidationException error = await Should.ThrowAsync<ValidationException>(() => ImportAsync(catalog));

        error.Message.ShouldContain("Duplicitní kód produktu: voucher-1-slot");
        error.Message.ShouldContain("cena musí být kladná");
        _repository.Products.ShouldBeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Produkt_s_neznamou_kategorii_neprojde()
    {
        CatalogDefinition catalog = Catalog(Voucher("voucher-1-slot", 690m) with { Category = "darky" });

        ValidationException error = await Should.ThrowAsync<ValidationException>(() => ImportAsync(catalog));

        error.Message.ShouldContain("kategorie 'darky' v katalogu není");
    }

    [Fact]
    public async Task Sazba_DPH_ktera_v_Cesku_neni_neprojde()
    {
        CatalogDefinition catalog = Catalog(Voucher("voucher-1-slot", 690m) with { VatRate = 15m });

        await Should.ThrowAsync<ValidationException>(() => ImportAsync(catalog));
    }

    private Task<CatalogImportResult> ImportAsync(CatalogDefinition catalog) =>
        new ImportCatalogCommandHandler(_repository, _unitOfWork, _clock)
            .HandleAsync(new ImportCatalogCommand(EnterpriseId, catalog), TestContext.Current.CancellationToken);

    private static CatalogDefinition Catalog(params ProductDefinition[] products) =>
        new("CZK", [new CategoryDefinition("vouchery", Czech("Vouchery"))], products);

    private static ProductDefinition Voucher(string code, decimal price) =>
        new(code, "vouchery", Czech(code), Czech("Popis"), "/img/katalog/voucher.svg", price, 21m);

    private static Dictionary<string, string> Czech(string text) => new() { ["cs"] = text };

    /// <summary>Repozitář v paměti — import mezi dvěma voláními „uloží“ tím, že objekty drží.</summary>
    private sealed class InMemoryCatalogRepository : ICatalogRepository
    {
        public List<ProductCategory> Categories { get; } = [];

        public List<Product> Products { get; } = [];

        public List<PriceList> PriceLists { get; } = [];

        public Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(Guid enterpriseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductCategory>>([.. Categories.Where(c => c.EnterpriseId == enterpriseId)]);

        public Task<IReadOnlyList<Product>> GetProductsAsync(Guid enterpriseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>([.. Products.Where(p => p.EnterpriseId == enterpriseId)]);

        public Task<PriceList?> GetActivePriceListAsync(Guid enterpriseId, CancellationToken cancellationToken = default) =>
            Task.FromResult(PriceLists.Find(p => p.EnterpriseId == enterpriseId && p.IsActive));

        public void Add(ProductCategory category) => Categories.Add(category);

        public void Add(Product product) => Products.Add(product);

        public void Add(PriceList priceList) => PriceLists.Add(priceList);
    }
}
