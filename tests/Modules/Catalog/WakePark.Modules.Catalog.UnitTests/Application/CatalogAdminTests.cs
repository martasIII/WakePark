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

    // --- Akční slevy (ADR 0014) ---

    [Fact]
    public async Task Akce_zlevni_produkty_kategorie_v_e_shopu_a_cenu_zaokrouhli_nahoru()
    {
        Guid productId = await VoucherWithPriceAsync(1490m);
        Guid categoryId = _repository.Products.Single(p => p.Id == productId).CategoryId;

        CatalogAdminDto admin = await CreatePromotionAsync("Vánoční akce", categoryId, 15, Today, Today.AddDays(10));

        AdminPromotionDto created = admin.Promotions.Single();
        (created.CategoryId, created.DiscountPercent, created.ValidFrom, created.ValidTo).ShouldBe((categoryId, 15, Today, Today.AddDays(10)));
        created.ProductDiscounts.ShouldBeEmpty();

        CatalogDto eshop = await EshopAsync();
        CatalogProductDto product = eshop.Products.Single();
        // 1 490 × 0,85 = 1 266,50 → nahoru na 1 267.
        product.UnitPrice.ShouldBe(1267m);
        product.RegularUnitPrice.ShouldBe(1490m);
        product.DiscountPercent.ShouldBe(15);
        eshop.Categories.Single().Promotion.ShouldNotBeNull().DiscountPercent.ShouldBe(15);
        eshop.Categories.Single().Promotion!.Name["cs"].ShouldBe("Vánoční akce");
        eshop.Categories.Single().Promotion!.ValidTo.ShouldBe(Today.AddDays(10));
        eshop.Categories.Single().Promotion!.UpTo.ShouldBeFalse();
        eshop.Categories.Single().Promotion!.AllProducts.ShouldBeTrue();

        // Jen e-shop: ceník na webu ukazuje běžnou cenu.
        (await PriceListAsync()).Sections.Single().Items.Single().UnitPrice.ShouldBe(1490m);
    }

    [Fact]
    public async Task Akce_plati_cele_dny_od_pulnoci_do_pulnoci_v_Praze()
    {
        Guid productId = await VoucherWithPriceAsync(1000m);
        Guid categoryId = _repository.Products.Single(p => p.Id == productId).CategoryId;
        await CreatePromotionAsync("Víkend", categoryId, 20, new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 26));

        (await EshopAsync()).Products.Single().UnitPrice.ShouldBe(1000m);

        // 24. 10. 22:30 UTC = 25. 10. 00:30 v Praze → akce už běží.
        _time.Set(new DateTimeOffset(2026, 10, 24, 22, 30, 0, TimeSpan.Zero));
        (await EshopAsync()).Products.Single().UnitPrice.ShouldBe(800m);

        // 26. 10. 21:30 UTC = 26. 10. 22:30 v Praze → poslední den ještě platí.
        _time.Set(new DateTimeOffset(2026, 10, 26, 21, 30, 0, TimeSpan.Zero));
        (await EshopAsync()).Products.Single().UnitPrice.ShouldBe(800m);

        // 26. 10. 23:30 UTC = 27. 10. 00:30 v Praze → po akci.
        _time.Set(new DateTimeOffset(2026, 10, 26, 23, 30, 0, TimeSpan.Zero));
        CatalogProductDto after = (await EshopAsync()).Products.Single();
        after.UnitPrice.ShouldBe(1000m);
        after.RegularUnitPrice.ShouldBeNull();
        after.DiscountPercent.ShouldBeNull();
    }

    [Fact]
    public async Task Akce_nadrazene_kategorie_plati_i_v_podkategorii()
    {
        CatalogAdminDto catalog = await CreateCategoryAsync("vouchery");
        Guid parentId = catalog.Categories.Single().Id;
        catalog = await Handler<CreateCategoryCommand>(new CreateCategoryCommand(
            new CreateCategoryRequest("darkove", parentId, Czech("Dárkové"), null, 0)));
        Guid childId = catalog.Categories.Single(c => c.Code == "darkove").Id;
        catalog = await CreateProductAsync("voucher-2-sloty", childId, Everywhere);
        await SetPriceAsync(catalog.Products.Single().Id, 1000m);

        await CreatePromotionAsync("Akce", parentId, 10, Today, Today);

        CatalogDto eshop = await EshopAsync();
        eshop.Products.Single().UnitPrice.ShouldBe(900m);
        eshop.Categories.Single().Promotion.ShouldNotBeNull();
    }

    [Fact]
    public async Task Prekryvajici_se_akce_na_stejnou_kategorii_nejdou_navazujici_ano()
    {
        Guid productId = await VoucherWithPriceAsync(1000m);
        Guid categoryId = _repository.Products.Single(p => p.Id == productId).CategoryId;
        await CreatePromotionAsync("Listopad", categoryId, 10, Today, Today.AddDays(6));

        DomainException overlap = await Should.ThrowAsync<DomainException>(
            () => CreatePromotionAsync("Black Friday", categoryId, 30, Today.AddDays(6), Today.AddDays(8)));
        overlap.Code.ShouldBe("promotion.overlap");

        CatalogAdminDto admin = await CreatePromotionAsync("Prosinec", categoryId, 20, Today.AddDays(7), Today.AddDays(30));
        admin.Promotions.Count.ShouldBe(2);

        // Ani úpravou se akce nesmí dostat do jiné.
        Guid december = admin.Promotions.Single(p => p.Name["cs"] == "Prosinec").Id;
        (await Should.ThrowAsync<DomainException>(() => Handler<UpdatePromotionCommand>(new UpdatePromotionCommand(
                december, new PromotionRequest(Czech("Prosinec"), categoryId, 20, Today.AddDays(3), Today.AddDays(30))))))
            .Code.ShouldBe("promotion.overlap");
    }

    [Fact]
    public async Task Akci_jde_upravit_a_smazat_kategorii_s_akci_smazat_nejde()
    {
        Guid productId = await VoucherWithPriceAsync(1000m);
        Guid categoryId = _repository.Products.Single(p => p.Id == productId).CategoryId;
        CatalogAdminDto admin = await CreatePromotionAsync("Akce", categoryId, 10, Today, Today.AddDays(3));
        Guid promotionId = admin.Promotions.Single().Id;

        admin = await Handler<UpdatePromotionCommand>(new UpdatePromotionCommand(
            promotionId, new PromotionRequest(Czech("Větší akce"), categoryId, 25, Today, Today.AddDays(5))));
        admin.Promotions.Single().DiscountPercent.ShouldBe(25);
        (await EshopAsync()).Products.Single().UnitPrice.ShouldBe(750m);

        _repository.Products.Clear();
        (await Should.ThrowAsync<DomainException>(() => Handler<DeleteCategoryCommand>(new DeleteCategoryCommand(categoryId))))
            .Code.ShouldBe("category.has_promotions");

        admin = await Handler<DeletePromotionCommand>(new DeletePromotionCommand(promotionId));
        admin.Promotions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Produkty_s_vlastnim_procentem_a_pruh_ukaze_az_nejvyssi_slevu()
    {
        CatalogAdminDto catalog = await CreateCategoryAsync("vouchery");
        Guid categoryId = catalog.Categories.Single().Id;
        Guid one = await ProductWithPriceAsync("voucher-1-slot", categoryId, 1000m);
        Guid two = await ProductWithPriceAsync("voucher-2-sloty", categoryId, 2000m);
        Guid six = await ProductWithPriceAsync("voucher-6-slotu", categoryId, 5000m);

        CatalogAdminDto admin = await CreatePromotionAsync("Vánoční akce", categoryId, 15, Today, Today.AddDays(10),
            [new ProductDiscountDto(six, 30)]);
        admin.Promotions.Single().ProductDiscounts.ShouldBe([new ProductDiscountDto(six, 30)]);

        CatalogDto eshop = await EshopAsync();
        eshop.Products.Single(p => p.Id == one).UnitPrice.ShouldBe(850m);
        eshop.Products.Single(p => p.Id == two).UnitPrice.ShouldBe(1700m);
        eshop.Products.Single(p => p.Id == six).UnitPrice.ShouldBe(3500m);
        eshop.Products.Single(p => p.Id == six).DiscountPercent.ShouldBe(30);
        CatalogPromotionDto banner = eshop.Categories.Single().Promotion.ShouldNotBeNull();
        (banner.DiscountPercent, banner.UpTo, banner.AllProducts).ShouldBe((30, true, true));

        // Jeden produkt z akce vyřazený (0 %), ostatní stejně → „−15 % na vybrané produkty“.
        await Handler<UpdatePromotionCommand>(new UpdatePromotionCommand(admin.Promotions.Single().Id,
            new PromotionRequest(Czech("Vánoční akce"), categoryId, 15, Today, Today.AddDays(10), [new ProductDiscountDto(one, 0)])));

        eshop = await EshopAsync();
        CatalogProductDto excluded = eshop.Products.Single(p => p.Id == one);
        (excluded.UnitPrice, excluded.RegularUnitPrice, excluded.DiscountPercent).ShouldBe((1000m, null, null));
        banner = eshop.Categories.Single().Promotion.ShouldNotBeNull();
        (banner.DiscountPercent, banner.UpTo, banner.AllProducts).ShouldBe((15, false, false));
    }

    [Fact]
    public async Task Vsechny_produkty_vyrazene_z_akce_pruh_neukazou()
    {
        Guid productId = await VoucherWithPriceAsync(1000m);
        Guid categoryId = _repository.Products.Single(p => p.Id == productId).CategoryId;

        await CreatePromotionAsync("Akce", categoryId, 15, Today, Today, [new ProductDiscountDto(productId, 0)]);

        CatalogDto eshop = await EshopAsync();
        eshop.Products.Single().DiscountPercent.ShouldBeNull();
        eshop.Categories.Single().Promotion.ShouldBeNull();
    }

    [Fact]
    public async Task Vlastni_procento_jen_pro_produkty_primo_v_kategorii_akce()
    {
        CatalogAdminDto catalog = await CreateCategoryAsync("vouchery");
        Guid parentId = catalog.Categories.Single().Id;
        catalog = await Handler<CreateCategoryCommand>(new CreateCategoryCommand(
            new CreateCategoryRequest("darkove", parentId, Czech("Dárkové"), null, 0)));
        Guid childId = catalog.Categories.Single(c => c.Code == "darkove").Id;
        Guid inChild = await ProductWithPriceAsync("voucher-2-sloty", childId, 1000m);

        (await Should.ThrowAsync<DomainException>(() => CreatePromotionAsync("Akce", parentId, 10, Today, Today,
                [new ProductDiscountDto(inChild, 30)])))
            .Code.ShouldBe("promotion.product_not_in_category");
    }

    private Task<CatalogAdminDto> CreatePromotionAsync(
        string name, Guid categoryId, int percent, DateOnly from, DateOnly to, IReadOnlyList<ProductDiscountDto>? discounts = null) =>
        Handler<CreatePromotionCommand>(new CreatePromotionCommand(new PromotionRequest(Czech(name), categoryId, percent, from, to, discounts)));

    private async Task<Guid> ProductWithPriceAsync(string code, Guid categoryId, decimal price)
    {
        CatalogAdminDto catalog = await CreateProductAsync(code, categoryId, Everywhere);
        Guid productId = catalog.Products.Single(p => p.Code == code).Id;
        await SetPriceAsync(productId, price);
        return productId;
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
            CreatePromotionCommand c => new CreatePromotionCommandHandler(loader).HandleAsync(c, ct),
            UpdatePromotionCommand c => new UpdatePromotionCommandHandler(loader).HandleAsync(c, ct),
            DeletePromotionCommand c => new DeletePromotionCommandHandler(loader).HandleAsync(c, ct),
            _ => throw new NotSupportedException(typeof(TCommand).Name),
        };
    }

    private CatalogWorkspaceLoader Loader() => new(_repository, _unitOfWork, _tenant, _time.Calendar);

    private static Dictionary<string, string> Czech(string text) => new() { ["cs"] = text };
}
