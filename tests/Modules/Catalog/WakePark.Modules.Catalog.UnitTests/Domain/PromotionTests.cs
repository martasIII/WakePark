using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Catalog.Domain.Common;
using WakePark.Modules.Catalog.Domain.Promotions;

namespace WakePark.Modules.Catalog.UnitTests.Domain;

/// <summary>Akční sleva: pravidla zadání, překryv a výpočet ceny (ADR 0014).</summary>
public sealed class PromotionTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 20);

    [Theory]
    [InlineData(1490, 15, 1267)]   // 1 266,50 → nahoru
    [InlineData(990, 15, 842)]     // 841,50 → nahoru
    [InlineData(1000, 20, 800)]    // přesně
    [InlineData(3890, 10, 3501)]   // 3 501,00
    [InlineData(19, 1, 19)]        // 18,81 → 19: levná položka se po zaokrouhlení nezlevní
    public void Cena_po_sleve_se_zaokrouhli_na_koruny_nahoru(decimal price, int percent, decimal expected) =>
        Create(percent).Apply(Guid.CreateVersion7(), price).ShouldBe(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    [InlineData(-5)]
    public void Sleva_musi_byt_1_az_90_procent(int percent) =>
        Should.Throw<DomainException>(() => Create(percent)).Code.ShouldBe("promotion.invalid_discount");

    [Fact]
    public void Konec_nesmi_byt_pred_zacatkem_ani_v_minulosti()
    {
        Should.Throw<DomainException>(() => Create(10, Today.AddDays(2), Today.AddDays(1))).Code.ShouldBe("promotion.invalid_range");
        Should.Throw<DomainException>(() => Create(10, Today.AddDays(-5), Today.AddDays(-1))).Code.ShouldBe("promotion.already_ended");
    }

    [Fact]
    public void Jednodenni_akce_a_akce_konci_dnes_jsou_v_poradku()
    {
        Create(10, Today, Today).IsActiveOn(Today).ShouldBeTrue();
        Create(10, Today.AddDays(-3), Today).IsActiveOn(Today).ShouldBeTrue();
    }

    [Fact]
    public void Platnost_je_vcetne_prvniho_i_posledniho_dne()
    {
        Promotion promotion = Create(10, Today, Today.AddDays(2));

        promotion.IsActiveOn(Today.AddDays(-1)).ShouldBeFalse();
        promotion.IsActiveOn(Today).ShouldBeTrue();
        promotion.IsActiveOn(Today.AddDays(2)).ShouldBeTrue();
        promotion.IsActiveOn(Today.AddDays(3)).ShouldBeFalse();
    }

    [Fact]
    public void Prekryv_je_jen_na_stejne_kategorii_a_se_spolecnym_dnem()
    {
        Promotion november = Create(10, Today, Today.AddDays(5));

        november.Overlaps(Create(10, Today.AddDays(5), Today.AddDays(9))).ShouldBeTrue();
        november.Overlaps(Create(10, Today.AddDays(6), Today.AddDays(9))).ShouldBeFalse();
        november.Overlaps(Promotion.Create(EnterpriseId, LocalizedText.Czech("Jiná"), Guid.CreateVersion7(), 10, [], Today, Today, Today)).ShouldBeFalse();
        november.Overlaps(november).ShouldBeFalse();
    }

    [Fact]
    public void Nazev_nejvys_60_znaku() =>
        Should.Throw<DomainException>(() => Promotion.Create(
                EnterpriseId, LocalizedText.Czech(new string('a', 61)), CategoryId, 10, [], Today, Today, Today))
            .Code.ShouldBe("promotion.name_too_long");

    [Fact]
    public void Produkt_s_vlastnim_procentem_ma_svou_slevu_ostatni_vychozi()
    {
        Guid top = Guid.CreateVersion7();
        Guid excluded = Guid.CreateVersion7();
        Promotion promotion = Create(15, discounts: [new ProductDiscount(top, 30), new ProductDiscount(excluded, 0)]);

        promotion.Apply(top, 1000m).ShouldBe(700m);
        promotion.Apply(excluded, 1000m).ShouldBe(1000m);
        promotion.Apply(Guid.CreateVersion7(), 1000m).ShouldBe(850m);
    }

    [Fact]
    public void Vlastni_procento_stejne_jako_vychozi_se_neuklada() =>
        Create(15, discounts: [new ProductDiscount(Guid.CreateVersion7(), 15)]).ProductDiscounts.ShouldBeEmpty();

    [Theory]
    [InlineData(-1)]
    [InlineData(91)]
    public void Vlastni_procento_musi_byt_0_az_90(int percent) =>
        Should.Throw<DomainException>(() => Create(15, discounts: [new ProductDiscount(Guid.CreateVersion7(), percent)]))
            .Code.ShouldBe("promotion.invalid_product_discount");

    [Fact]
    public void Produkt_muze_mit_jen_jedno_vlastni_procento()
    {
        Guid product = Guid.CreateVersion7();
        Should.Throw<DomainException>(() => Create(15, discounts: [new ProductDiscount(product, 20), new ProductDiscount(product, 30)]))
            .Code.ShouldBe("promotion.duplicate_product");
    }

    private static Promotion Create(int percent, DateOnly? from = null, DateOnly? to = null, IEnumerable<ProductDiscount>? discounts = null) =>
        Promotion.Create(EnterpriseId, LocalizedText.Czech("Akce"), CategoryId, percent, discounts ?? [], from ?? Today, to ?? Today.AddDays(7), Today);
}
