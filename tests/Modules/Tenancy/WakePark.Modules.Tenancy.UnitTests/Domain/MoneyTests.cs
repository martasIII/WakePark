using Shouldly;

using WakePark.BuildingBlocks.Domain.ValueObjects;

namespace WakePark.Modules.Tenancy.UnitTests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void Secteni_stejnych_men_projde()
    {
        Money result = new Money(100.50m, "CZK").Add(new Money(49.50m, "CZK"));

        result.Amount.ShouldBe(150.00m);
        result.Currency.ShouldBe("CZK");
    }

    [Fact]
    public void Secteni_ruznych_men_je_chyba()
    {
        Money czk = new(100m, "CZK");
        Money eur = new(100m, "EUR");

        Should.Throw<InvalidOperationException>(() => czk.Add(eur));
    }

    [Fact]
    public void Castka_se_zaokrouhli_na_dve_desetinna_mista()
    {
        Money money = new(10.005m, "CZK");

        money.Amount.ShouldBe(10.00m);
    }

    [Fact]
    public void Rovnost_se_urcuje_podle_hodnoty_ne_podle_reference()
    {
        new Money(10m, "CZK").ShouldBe(new Money(10m, "CZK"));
    }
}
