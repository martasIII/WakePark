using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application;
using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Application.Admin;
using WakePark.Modules.Catalog.Application.Catalog;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Catalog.UnitTests.Application;

/// <summary>
/// Správa katalogu v backoffice a to, co z ní uvidí web: e-shop a ceník čtou stejná data
/// a stejné „dnes“ (ADR 0009).
/// </summary>
public sealed class CatalogAdminTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 10, 24);
    private static readonly ProductVisibilityDto Everywhere = new(PriceList: true, Eshop: true, Pos: true);
    private static readonly ProductVisibilityDto PriceListAndPos = new(PriceList: true, Eshop: false, Pos: true);

    private readonly InMemoryCatalogRepository _repository = new();
    private readonly ICatalogUnitOfWork _unitOfWork = Substitute.For<ICatalogUnitOfWork>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly TestCalendar _time = new(new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero));

    public CatalogAdminTests()
    {
        _tenant.EnterpriseId.Returns(EnterpriseId);
    }

    [Fact]
    public async Task Admin_zalozi_kategorii_produkt_a_cenu_a_web_je_hned_vidi()
    {
        CatalogAdminDto catalog = await CreateCategoryAsync("vouchery");
        Guid categoryId = catalog.Categories.Single().Id;
        catalog = await CreateProductAsync("voucher-1-slot", categoryId, Everywhere);
        Guid productId = catalog.Products.Single().Id;

        catalog = await SetPriceAsync(productId, 690m);

        catalog.Products.Single().CurrentPrice.ShouldBe(new PriceDto(690m, 12m, Today));
        (await EshopAsync()).Products.Single().UnitPrice.ShouldBe(690m);
        (await PriceListAsync()).Sections.Single().Items.Single().UnitPrice.ShouldBe(690m);
        await _unitOfWork.Received(3).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cena_od_budouciho_dne_se_na_webu_neukaze_dokud_neplati()
    {
        Guid productId = await VoucherWithPriceAsync(690m);
        DateOnly season = new(2027, 5, 1);

        CatalogAdminDto catalog = await SetPriceAsync(productId, 790m, season);

        catalog.Products.Single().ScheduledPrices.ShouldBe([new PriceDto(790m, 12m, season)]);
        (await EshopAsync()).Products.Single().UnitPrice.ShouldBe(690m);

        _time.Set(new DateTimeOffset(2027, 5, 1, 6, 0, 0, TimeSpan.Zero));
        (await EshopAsync()).Products.Single().UnitPrice.ShouldBe(790m);
    }

    [Fact]
    public async Task Cena_plati_od_pulnoci_v_Praze_ne_v_UTC()
    {
        Guid productId = await VoucherWithPriceAsync(690m);
        await SetPriceAsync(productId, 790m, new DateOnly(2026, 10, 25));

        // 24. 10. 22:30 UTC je v Praze už 25. 10. 00:30.
        _time.Set(new DateTimeOffset(2026, 10, 24, 22, 30, 0, TimeSpan.Zero));

        (await EshopAsync()).Products.Single().UnitPrice.ShouldBe(790m);
    }

    [Fact]
    public async Task Naplanovanou_cenu_jde_zrusit()
    {
        Guid productId = await VoucherWithPriceAsync(690m);
        DateOnly season = new(2027, 5, 1);
        await SetPriceAsync(productId, 790m, season);

        CatalogAdminDto catalog = await Handler<RemoveScheduledPriceCommand>(new RemoveScheduledPriceCommand(productId, season));

        catalog.Products.Single().ScheduledPrices.ShouldBeEmpty();
    }

    [Fact]
    public async Task Produkt_v_e_shopu_nesmi_mit_nulovou_cenu()
    {
        Guid productId = await VoucherWithPriceAsync(690m);

        DomainException error = await Should.ThrowAsync<DomainException>(() => SetPriceAsync(productId, 0m));

        error.Code.ShouldBe("product.eshop_requires_price");
    }

    [Fact]
    public async Task Helma_zdarma_je_v_ceniku_ale_ne_v_e_shopu()
    {
        CatalogAdminDto catalog = await CreateCategoryAsync("pujcovna", description: "Po dobu jízdného.");
        catalog = await CreateProductAsync("helma", catalog.Categories.Single().Id, PriceListAndPos);
        await SetPriceAsync(catalog.Products.Single().Id, 0m);

        (await EshopAsync()).Products.ShouldBeEmpty();
        PriceListSectionDto section = (await PriceListAsync()).Sections.Single();
        section.Description!["cs"].ShouldBe("Po dobu jízdného.");
        section.Items.Single().UnitPrice.ShouldBe(0m);
    }

    [Fact]
    public async Task Cenik_ukaze_podsekce_a_sekci_jen_s_popisem_ale_ne_prazdnou()
    {
        CatalogAdminDto catalog = await CreateCategoryAsync("permanentky");
        Guid permanentky = catalog.Categories.Single().Id;
        catalog = await Handler<CreateCategoryCommand>(new CreateCategoryCommand(
            new CreateCategoryRequest("kratke", permanentky, Czech("Krátké"), null, 0)));
        Guid kratke = catalog.Categories.Single(c => c.Code == "kratke").Id;
        await CreateCategoryAsync("firemni-akce", description: "Napište nám.", sortOrder: 2);
        await CreateCategoryAsync("prazdna", sortOrder: 3);
        catalog = await CreateProductAsync("permanentka-10x20", kratke, PriceListAndPos);
        await SetPriceAsync(catalog.Products.Single().Id, 4200m);

        PublicPriceListDto priceList = await PriceListAsync();

        priceList.Sections.Select(s => s.Code).ShouldBe(["permanentky", "firemni-akce"]);
        priceList.Sections[0].Items.ShouldBeEmpty();
        priceList.Sections[0].Subsections.Single().Items.Single().Code.ShouldBe("permanentka-10x20");
        priceList.Sections[1].Description!["cs"].ShouldBe("Napište nám.");
    }

    [Fact]
    public async Task Skryty_produkt_zmizi_z_webu_ale_admin_ho_dal_vidi()
    {
        Guid productId = await VoucherWithPriceAsync(690m);
        AdminProductDto product = (await AdminAsync()).Products.Single();

        CatalogAdminDto catalog = await Handler<UpdateProductCommand>(new UpdateProductCommand(productId, new UpdateProductRequest(
            product.CategoryId, product.Name, product.Description, product.Badge, product.ImagePath, product.SortOrder,
            product.Visibility, IsActive: false)));

        catalog.Products.Single().IsActive.ShouldBeFalse();
        (await EshopAsync()).Products.ShouldBeEmpty();
        (await PriceListAsync()).Sections.ShouldBeEmpty();
    }

    [Fact]
    public async Task Kod_musi_byt_unikatni()
    {
        await CreateCategoryAsync("vouchery");

        DomainException error = await Should.ThrowAsync<DomainException>(() => CreateCategoryAsync("vouchery"));

        error.Code.ShouldBe("category.duplicate_code");
    }

    [Fact]
    public async Task Kategorii_s_produkty_nejde_smazat_prazdnou_ano()
    {
        Guid productId = await VoucherWithPriceAsync(690m);
        Guid categoryId = _repository.Products.Single(p => p.Id == productId).CategoryId;
        CatalogAdminDto catalog = await CreateCategoryAsync("prazdna");
        Guid emptyId = catalog.Categories.Single(c => c.Code == "prazdna").Id;

        (await Should.ThrowAsync<DomainException>(() => Handler<DeleteCategoryCommand>(new DeleteCategoryCommand(categoryId))))
            .Code.ShouldBe("category.not_empty");

        catalog = await Handler<DeleteCategoryCommand>(new DeleteCategoryCommand(emptyId));
        catalog.Categories.Select(c => c.Code).ShouldBe(["vouchery"]);
    }

    [Fact]
    public async Task Produkt_jineho_provozovatele_neexistuje()
    {
        Guid productId = await VoucherWithPriceAsync(690m);
        _tenant.EnterpriseId.Returns(Guid.CreateVersion7());

        await Should.ThrowAsync<NotFoundException>(() => SetPriceAsync(productId, 790m));
    }

    [Fact]
    public async Task Bez_provozovatele_v_prihlaseni_neni_co_spravovat()
    {
        _tenant.EnterpriseId.Returns((Guid?)null);

        await Should.ThrowAsync<NotFoundException>(AdminAsync);
    }

    private async Task<Guid> VoucherWithPriceAsync(decimal price)
    {
        CatalogAdminDto catalog = await CreateCategoryAsync("vouchery");
        catalog = await CreateProductAsync("voucher-1-slot", catalog.Categories.Single().Id, Everywhere);
        Guid productId = catalog.Products.Single().Id;
        await SetPriceAsync(productId, price);
        return productId;
    }

    private Task<CatalogAdminDto> CreateCategoryAsync(string code, string? description = null, int sortOrder = 0) =>
        Handler<CreateCategoryCommand>(new CreateCategoryCommand(new CreateCategoryRequest(
            code, null, Czech(code), description is null ? null : Czech(description), sortOrder)));

    private Task<CatalogAdminDto> CreateProductAsync(string code, Guid categoryId, ProductVisibilityDto visibility) =>
        Handler<CreateProductCommand>(new CreateProductCommand(new CreateProductRequest(
            code, categoryId, Czech(code), Czech("Popis"), null, null, 0, visibility)));

    private Task<CatalogAdminDto> SetPriceAsync(Guid productId, decimal price, DateOnly? validFrom = null) =>
        Handler<SetProductPriceCommand>(new SetProductPriceCommand(productId, new SetPriceRequest(price, 12m, validFrom)));

    private Task<CatalogAdminDto> AdminAsync() =>
        new GetCatalogAdminQueryHandler(Loader()).HandleAsync(new GetCatalogAdminQuery(), TestContext.Current.CancellationToken);

    private Task<CatalogDto> EshopAsync() =>
        new CatalogReader(_repository, _time.Calendar).GetPublishedCatalogAsync(EnterpriseId, TestContext.Current.CancellationToken);

    private Task<PublicPriceListDto> PriceListAsync() =>
        new GetPublicPriceListQueryHandler(_repository, _time.Calendar)
            .HandleAsync(new GetPublicPriceListQuery(EnterpriseId), TestContext.Current.CancellationToken);

    private Task<CatalogAdminDto> Handler<TCommand>(TCommand command)
    {
        CatalogWorkspaceLoader loader = Loader();
        CancellationToken ct = TestContext.Current.CancellationToken;

        return command switch
        {
            CreateCategoryCommand c => new CreateCategoryCommandHandler(loader).HandleAsync(c, ct),
            UpdateCategoryCommand c => new UpdateCategoryCommandHandler(loader).HandleAsync(c, ct),
            DeleteCategoryCommand c => new DeleteCategoryCommandHandler(loader).HandleAsync(c, ct),
            CreateProductCommand c => new CreateProductCommandHandler(loader).HandleAsync(c, ct),
            UpdateProductCommand c => new UpdateProductCommandHandler(loader).HandleAsync(c, ct),
            SetProductPriceCommand c => new SetProductPriceCommandHandler(loader).HandleAsync(c, ct),
            RemoveScheduledPriceCommand c => new RemoveScheduledPriceCommandHandler(loader).HandleAsync(c, ct),
            _ => throw new NotSupportedException(typeof(TCommand).Name),
        };
    }

    private CatalogWorkspaceLoader Loader() => new(_repository, _unitOfWork, _tenant, _time.Calendar);

    private static Dictionary<string, string> Czech(string text) => new() { ["cs"] = text };
}
