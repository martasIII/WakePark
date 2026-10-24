using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Catalog.Domain.Common;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.UnitTests.Domain;

public sealed class ProductAndPriceListTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 10, 24);

    [Theory]
    [InlineData("https://example.com/a.png")]
    [InlineData("//example.com/a.png")]
    [InlineData("/img/../../etc/passwd")]
    [InlineData("img/a.png")]
    public void Obrazek_musi_byt_cesta_na_vlastnim_webu(string imagePath)
    {
        Should.Throw<DomainException>(() => CreateProduct(imagePath));
    }

    [Fact]
    public void Stejne_udaje_nejsou_zmena()
    {
        Product product = CreateProduct("/img/a.svg");

        product.Update(Details("/img/a.svg")).ShouldBeFalse();
        product.Update(Details("/img/a.svg") with { Channels = ProductChannels.PriceList }).ShouldBeTrue();
    }

    [Fact]
    public void Popis_je_nepovinny()
    {
        Product product = Product.Create(EnterpriseId, "helma", Details(null) with { Description = null });

        product.Description.ShouldBeNull();
    }

    [Fact]
    public void Produkt_se_nabizi_jen_tam_kde_ma_byt_a_jen_aktivni()
    {
        Product product = Product.Create(EnterpriseId, "helma", Details(null) with { Channels = ProductChannels.PriceList | ProductChannels.Pos });

        product.IsOfferedIn(ProductChannels.PriceList).ShouldBeTrue();
        product.IsOfferedIn(ProductChannels.Eshop).ShouldBeFalse();

        product.Deactivate();
        product.IsOfferedIn(ProductChannels.PriceList).ShouldBeFalse();
    }

    [Fact]
    public void Skryty_produkt_jde_znovu_zobrazit()
    {
        Product product = CreateProduct(null);

        product.Deactivate().ShouldBeTrue();
        product.Deactivate().ShouldBeFalse();
        product.Activate().ShouldBeTrue();
        product.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Cena_od_dneska_se_zalozi_a_stejna_neni_zmena()
    {
        PriceList priceList = CreatePriceList();
        Guid productId = Guid.CreateVersion7();

        priceList.Currency.ShouldBe("CZK");
        priceList.SetPrice(productId, 690m, 12m, Today, Today).ShouldBeTrue();
        priceList.SetPrice(productId, 690m, 12m, Today, Today).ShouldBeFalse();

        priceList.PriceOn(productId, Today)!.UnitPrice.ShouldBe(690m);
    }

    [Fact]
    public void Zmena_ceny_tentyz_den_prepise_dnesni_cenu()
    {
        PriceList priceList = CreatePriceList();
        Guid productId = Guid.CreateVersion7();

        priceList.SetPrice(productId, 690m, 12m, Today, Today);
        priceList.SetPrice(productId, 750m, 12m, Today, Today).ShouldBeTrue();

        priceList.Items.Count.ShouldBe(1);
        priceList.PriceOn(productId, Today)!.UnitPrice.ShouldBe(750m);
    }

    [Fact]
    public void Zmena_ceny_jiny_den_zachova_historii()
    {
        PriceList priceList = CreatePriceList();
        Guid productId = Guid.CreateVersion7();
        priceList.SetPrice(productId, 690m, 12m, Today, Today);

        DateOnly tomorrow = Today.AddDays(1);
        priceList.SetPrice(productId, 750m, 12m, tomorrow, tomorrow);

        priceList.Items.Count.ShouldBe(2);
        priceList.PriceOn(productId, Today)!.UnitPrice.ShouldBe(690m);
        priceList.PriceOn(productId, tomorrow)!.UnitPrice.ShouldBe(750m);
    }

    [Fact]
    public void Cena_od_budouciho_dne_plati_az_od_nej()
    {
        PriceList priceList = CreatePriceList();
        Guid productId = Guid.CreateVersion7();
        DateOnly season = new(2027, 5, 1);

        priceList.SetPrice(productId, 690m, 12m, Today, Today);
        priceList.SetPrice(productId, 790m, 12m, season, Today).ShouldBeTrue();

        priceList.PriceOn(productId, Today)!.UnitPrice.ShouldBe(690m);
        priceList.PriceOn(productId, season.AddDays(-1))!.UnitPrice.ShouldBe(690m);
        priceList.PriceOn(productId, season)!.UnitPrice.ShouldBe(790m);
        priceList.ScheduledPrices(productId, Today).Select(p => p.ValidFrom).ShouldBe([season]);
    }

    [Fact]
    public void Cenu_nejde_nastavit_zpetne()
    {
        PriceList priceList = CreatePriceList();

        DomainException error = Should.Throw<DomainException>(() =>
            priceList.SetPrice(Guid.CreateVersion7(), 690m, 12m, Today.AddDays(-1), Today));

        error.Code.ShouldBe("price_list.valid_from_in_past");
    }

    [Fact]
    public void Nulova_cena_je_dovolena_helma_zdarma()
    {
        PriceList priceList = CreatePriceList();

        priceList.SetPrice(Guid.CreateVersion7(), 0m, 21m, Today, Today).ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1, 21)]
    [InlineData(100, -1)]
    [InlineData(100, 101)]
    public void Nesmyslna_cena_nebo_sazba_neprojde(decimal price, decimal vatRate)
    {
        PriceList priceList = CreatePriceList();

        Should.Throw<DomainException>(() => priceList.SetPrice(Guid.CreateVersion7(), price, vatRate, Today, Today));
    }

    [Fact]
    public void Zrusit_jde_jen_cenu_ktera_jeste_neplati()
    {
        PriceList priceList = CreatePriceList();
        Guid productId = Guid.CreateVersion7();
        DateOnly season = new(2027, 5, 1);
        priceList.SetPrice(productId, 690m, 12m, Today, Today);
        priceList.SetPrice(productId, 790m, 12m, season, Today);

        Should.Throw<DomainException>(() => priceList.RemoveScheduledPrice(productId, Today, Today));
        priceList.RemoveScheduledPrice(productId, season, Today).ShouldBeTrue();
        priceList.RemoveScheduledPrice(productId, season, Today).ShouldBeFalse();

        priceList.Items.Count.ShouldBe(1);
    }

    private static PriceList CreatePriceList() => PriceList.Create(EnterpriseId, "Ceník", "czk", Today);

    private static Product CreateProduct(string? imagePath) =>
        Product.Create(EnterpriseId, "voucher-1-slot", Details(imagePath));

    private static ProductDetails Details(string? imagePath) =>
        new(CategoryId, LocalizedText.Czech("Voucher"), LocalizedText.Czech("Popis"), null, imagePath, 0, ProductChannels.All);
}
