using FluentValidation;

using NSubstitute;

using Shouldly;

using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.UnitTests.Application;

public sealed class ImportCatalogCommandHandlerTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();

    private readonly InMemoryCatalogRepository _repository = new();
    private readonly ICatalogUnitOfWork _unitOfWork = Substitute.For<ICatalogUnitOfWork>();
    private readonly TestCalendar _time = new(new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Prvni_import_zalozi_kategorie_produkty_a_ceny()
    {
        CatalogImportResult result = await ImportAsync(Catalog(Voucher("voucher-1-slot", 690m), Voucher("voucher-2-sloty", 1290m)));

        result.ShouldBe(new CatalogImportResult(CatalogImportMode.AddMissing, Created: 2, Updated: 0, Deactivated: 0, Unchanged: 0));
        _repository.Categories.Count.ShouldBe(2);
        _repository.PriceLists.Single().Items.Count.ShouldBe(2);
        _repository.PriceLists.Single().Items.ShouldAllBe(i => i.ValidFrom == new DateOnly(2026, 10, 24));
        _repository.Products.Select(p => p.SortOrder).ShouldBe([0, 1]);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pri_nasazeni_import_existujici_produkty_neprepise()
    {
        await ImportAsync(Catalog(Voucher("voucher-1-slot", 690m)));
        Product product = _repository.Products.Single();
        product.Deactivate(); // admin produkt v backoffice skryl

        CatalogImportResult result = await ImportAsync(Catalog(Voucher("voucher-1-slot", 750m), Voucher("voucher-2-sloty", 1290m)));

        result.ShouldBe(new CatalogImportResult(CatalogImportMode.AddMissing, Created: 1, Updated: 0, Deactivated: 0, Unchanged: 1));
        product.IsActive.ShouldBeFalse("úpravy z backoffice import nepřepíše");
        _repository.PriceLists.Single().PriceOn(product.Id, new DateOnly(2026, 10, 24))!.UnitPrice.ShouldBe(690m);
    }

    [Fact]
    public async Task Prepis_srovna_databazi_se_souborem_a_chybejici_skryje()
    {
        await ImportAsync(Catalog(Voucher("voucher-1-slot", 690m), Voucher("voucher-2-sloty", 1290m)));

        CatalogImportResult result = await ImportAsync(Catalog(Voucher("voucher-1-slot", 750m)), CatalogImportMode.Overwrite);

        result.ShouldBe(new CatalogImportResult(CatalogImportMode.Overwrite, Created: 0, Updated: 1, Deactivated: 1, Unchanged: 0));
        _repository.Products.Count.ShouldBe(2, "produkt se nikdy nemaže, odkazují na něj prodané poukazy");
        _repository.Products.Single(p => p.Code == "voucher-2-sloty").IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Prepis_dalsi_den_zachova_historii_cen()
    {
        await ImportAsync(Catalog(Voucher("voucher-1-slot", 690m)));
        _time.Set(new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero));

        await ImportAsync(Catalog(Voucher("voucher-1-slot", 750m)), CatalogImportMode.Overwrite);

        Guid productId = _repository.Products.Single().Id;
        _repository.PriceLists.Single().PriceOn(productId, new DateOnly(2026, 10, 24))!.UnitPrice.ShouldBe(690m);
        _repository.PriceLists.Single().PriceOn(productId, new DateOnly(2026, 10, 25))!.UnitPrice.ShouldBe(750m);
    }

    [Fact]
    public async Task Podkategorie_se_zaradi_pod_rodice_bez_ohledu_na_poradi_v_souboru()
    {
        CatalogDefinition catalog = Catalog(Voucher("voucher-1-slot", 690m)) with
        {
            Categories =
            [
                new CategoryDefinition("kratke", Czech("Krátké"), Parent: "permanentky"),
                new CategoryDefinition("permanentky", Czech("Permanentky")),
                new CategoryDefinition("vouchery", Czech("Vouchery")),
            ],
        };

        await ImportAsync(catalog);

        Guid parentId = _repository.Categories.Single(c => c.Code == "permanentky").Id;
        _repository.Categories.Single(c => c.Code == "kratke").ParentId.ShouldBe(parentId);
    }

    [Fact]
    public async Task Mista_zobrazeni_se_prevedou_z_textu()
    {
        await ImportAsync(Catalog(Voucher("helma", 0m) with { ShowIn = ["pricelist", "pos"] }));

        _repository.Products.Single().Channels.ShouldBe(ProductChannels.PriceList | ProductChannels.Pos);
    }

    [Fact]
    public async Task Neplatny_soubor_neulozi_nic()
    {
        CatalogDefinition catalog = Catalog(Voucher("voucher-1-slot", 690m), Voucher("voucher-1-slot", 0m));

        ValidationException error = await Should.ThrowAsync<ValidationException>(() => ImportAsync(catalog));

        error.Message.ShouldContain("Duplicitní kód produktu: voucher-1-slot");
        error.Message.ShouldContain("v e-shopu musí mít kladnou cenu");
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
    public async Task Nezname_misto_zobrazeni_neprojde()
    {
        CatalogDefinition catalog = Catalog(Voucher("voucher-1-slot", 690m) with { ShowIn = ["web"] });

        ValidationException error = await Should.ThrowAsync<ValidationException>(() => ImportAsync(catalog));

        error.Message.ShouldContain("neznámé místo zobrazení 'web'");
    }

    [Fact]
    public async Task Treti_uroven_kategorii_neprojde()
    {
        CatalogDefinition catalog = Catalog(Voucher("voucher-1-slot", 690m)) with
        {
            Categories =
            [
                new CategoryDefinition("vouchery", Czech("Vouchery")),
                new CategoryDefinition("darkove", Czech("Dárkové"), Parent: "vouchery"),
                new CategoryDefinition("vanocni", Czech("Vánoční"), Parent: "darkove"),
            ],
        };

        ValidationException error = await Should.ThrowAsync<ValidationException>(() => ImportAsync(catalog));

        error.Message.ShouldContain("Kategorie 'vanocni'");
    }

    [Fact]
    public async Task Sazba_DPH_ktera_v_Cesku_neni_neprojde()
    {
        CatalogDefinition catalog = Catalog(Voucher("voucher-1-slot", 690m) with { VatRate = 15m });

        await Should.ThrowAsync<ValidationException>(() => ImportAsync(catalog));
    }

    private Task<CatalogImportResult> ImportAsync(CatalogDefinition catalog, CatalogImportMode mode = CatalogImportMode.AddMissing) =>
        new ImportCatalogCommandHandler(_repository, _unitOfWork, _time.Calendar)
            .HandleAsync(new ImportCatalogCommand(EnterpriseId, catalog, mode), TestContext.Current.CancellationToken);

    private static CatalogDefinition Catalog(params ProductDefinition[] products) =>
        new(
            "CZK",
            null,
            [new CategoryDefinition("vouchery", Czech("Vouchery")), new CategoryDefinition("permanentky", Czech("Permanentky"))],
            products);

    private static ProductDefinition Voucher(string code, decimal price) =>
        new(code, "vouchery", Czech(code), Czech("Popis"), "/img/katalog/voucher.svg", price, 21m, ["pricelist", "eshop", "pos"]);

    private static Dictionary<string, string> Czech(string text) => new() { ["cs"] = text };
}
