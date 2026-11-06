using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Billing.Domain.Customers;
using WakePark.Modules.Billing.Domain.Orders;

namespace WakePark.Modules.Billing.UnitTests;

public sealed class DedicationTests
{
    [Theory]
    [InlineData("Pro Kristýnku k narozeninám!")]
    [InlineData("Für Jürgen — viel Spaß & 100 % Wellen…")]
    [InlineData("Pour Zoé, «bon anniversaire» (30)")]
    [InlineData("Dla Łukasza: wszystkiego najlepszego")]
    [InlineData("")]
    public void Povolene_venovani(string text)
    {
        Dedication.IsValid(Dedication.Normalize(text)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Všechno nejlepší 🎉")]
    [InlineData("Для Ивана")]
    [InlineData("<script>")]
    public void Emoji_jina_pisma_a_znacky_neprojdou(string text)
    {
        Dedication.IsValid(Dedication.Normalize(text)).ShouldBeFalse();
    }

    [Fact]
    public void Delka_nejvys_120_znaku()
    {
        Dedication.IsValid(new string('a', Dedication.MaxLength)).ShouldBeTrue();
        Dedication.IsValid(new string('a', Dedication.MaxLength + 1)).ShouldBeFalse();
    }

    [Fact]
    public void Zapis_se_sjednoti()
    {
        Dedication.Normalize("  Pro\r\n  Kristýnku \t ").ShouldBe("Pro Kristýnku");
        Dedication.Normalize("Kristýnka").ShouldBe("Kristýnka");   // rozložená diakritika (macOS)
        Dedication.Normalize(null).ShouldBe(string.Empty);
    }

    [Fact]
    public void Polozka_ma_venovani_pro_kazdy_kus()
    {
        Order order = Place(new OrderLineDraft(Guid.CreateVersion7(), "voucher-1-slot", "Voucher", "Voucher", 3, 690m, 12m, [" Pro Danu ", null]));

        order.Lines.Single().Dedications.ShouldBe(["Pro Danu", "", ""]);
    }

    [Fact]
    public void Vic_venovani_nez_kusu_neprojde()
    {
        Should.Throw<DomainException>(() =>
            Place(new OrderLineDraft(Guid.CreateVersion7(), "voucher-1-slot", "Voucher", "Voucher", 1, 690m, 12m, ["A", "B"])));
    }

    private static Order Place(OrderLineDraft line) => Order.Place(
        Guid.CreateVersion7(),
        261001,
        new string('a', Order.AccessTokenLength),
        CustomerContact.Create("Jana", "jana@example.cz", null, null, null, null, null),
        "cs",
        "CZK",
        [line],
        new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero),
        TimeSpan.FromDays(4));
}
