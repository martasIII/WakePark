using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.UnitTests.Domain;

public sealed class LocalizedTextTests
{
    [Fact]
    public void Bez_cestiny_neprojde()
    {
        Should.Throw<DomainException>(() =>
            LocalizedText.Create(new Dictionary<string, string> { ["en"] = "Voucher" }));
    }

    [Fact]
    public void Prazdny_preklad_se_zahodi_jako_by_chybel()
    {
        LocalizedText text = LocalizedText.Create(new Dictionary<string, string>
        {
            ["cs"] = " Voucher ",
            ["en"] = "  ",
        });

        text.Translations.Keys.ShouldBe(["cs"]);
        text.ToString().ShouldBe("Voucher");
    }

    [Theory]
    [InlineData("cze")]
    [InlineData("c")]
    [InlineData("c1")]
    public void Kod_jazyka_musi_mit_dve_pismena(string language)
    {
        Should.Throw<DomainException>(() => LocalizedText.Create(new Dictionary<string, string>
        {
            ["cs"] = "Voucher",
            [language] = "Voucher",
        }));
    }

    [Fact]
    public void Rovnost_nezavisi_na_poradi_jazyku()
    {
        LocalizedText first = LocalizedText.Create(new Dictionary<string, string> { ["cs"] = "A", ["en"] = "B" });
        LocalizedText second = LocalizedText.Create(new Dictionary<string, string> { ["en"] = "B", ["cs"] = "A" });

        first.ShouldBe(second);
    }

    [Theory]
    [InlineData("voucher-2-sloty", true)]
    [InlineData("permanentka-10x20", true)]
    [InlineData("Voucher", false)]
    [InlineData("voucher--2", false)]
    [InlineData("-voucher", false)]
    [InlineData("poukaz-pro-dva-é", false)]
    [InlineData("", false)]
    public void Kod_katalogu_jsou_jen_mala_pismena_cislice_a_pomlcky(string code, bool valid)
    {
        CatalogCode.IsValid(code).ShouldBe(valid);
    }
}
