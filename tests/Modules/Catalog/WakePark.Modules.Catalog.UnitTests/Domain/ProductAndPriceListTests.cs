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

        product.Update(CategoryId, LocalizedText.Czech("Voucher"), LocalizedText.Czech("Popis"), "/img/a.svg", 0)
            .ShouldBeFalse();
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
    public void Cena_se_zalozi_zmeni_a_stejna_neni_zmena()
    {
        PriceList priceList = PriceList.Create(EnterpriseId, "E-shop", "czk", new DateOnly(2026, 10, 21));
        Guid productId = Guid.CreateVersion7();

        priceList.Currency.ShouldBe("CZK");
        priceList.SetPrice(productId, 690m, 21m).ShouldBeTrue();
        priceList.SetPrice(productId, 690m, 21m).ShouldBeFalse();
        priceList.SetPrice(productId, 750m, 21m).ShouldBeTrue();

        priceList.Items.Count.ShouldBe(1);
        priceList.FindItem(productId)!.UnitPrice.ShouldBe(750m);
    }

    [Theory]
    [InlineData(0, 21)]
    [InlineData(-1, 21)]
    [InlineData(100, -1)]
    [InlineData(100, 101)]
    public void Nesmyslna_cena_nebo_sazba_neprojde(decimal price, decimal vatRate)
    {
        PriceList priceList = PriceList.Create(EnterpriseId, "E-shop", "CZK", new DateOnly(2026, 10, 21));

        Should.Throw<DomainException>(() => priceList.SetPrice(Guid.CreateVersion7(), price, vatRate));
    }

    private static Product CreateProduct(string? imagePath) =>
        Product.Create(
            EnterpriseId,
            "voucher-1-slot",
            CategoryId,
            LocalizedText.Czech("Voucher"),
            LocalizedText.Czech("Popis"),
            imagePath,
            0);
}
