using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Tenancy.Domain.Parks;

namespace WakePark.Modules.Reservations.UnitTests.Domain;

/// <summary>
/// Politika storna patří areálu — tady se ověřuje, že se podle ní opravdu počítá,
/// ne že je někde v kódu zadrátovaná konstanta.
/// </summary>
public sealed class CancellationPolicyTests
{
    private static readonly DateTimeOffset SlotStart = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    private static CancellationPolicy Policy(int freeHours = 24, decimal latePercent = 50m) =>
        new(cancellationAllowed: true, freeHours, latePercent, noShowRefundPercent: 0m);

    [Fact]
    public void Storno_ve_lhute_vraci_vse()
    {
        decimal refund = Policy().RefundPercentFor(SlotStart.AddHours(-48), SlotStart);

        refund.ShouldBe(100m);
    }

    [Fact]
    public void Storno_presne_na_hranici_lhuty_se_jeste_pocita_jako_vcasne()
    {
        decimal refund = Policy(freeHours: 24).RefundPercentFor(SlotStart.AddHours(-24), SlotStart);

        refund.ShouldBe(100m);
    }

    [Fact]
    public void Pozdni_storno_vraci_podle_nastaveni_arealu()
    {
        decimal refund = Policy(freeHours: 24, latePercent: 50m)
            .RefundPercentFor(SlotStart.AddHours(-2), SlotStart);

        refund.ShouldBe(50m);
    }

    [Fact]
    public void Areal_muze_storno_zakazat_uplne()
    {
        CancellationPolicy policy = new(
            cancellationAllowed: false,
            freeCancellationHours: 0,
            lateCancellationRefundPercent: 0m,
            noShowRefundPercent: 0m);

        Should.Throw<DomainException>(() => policy.RefundPercentFor(SlotStart.AddDays(-7), SlotStart))
            .Code.ShouldBe("reservation.cancellation_not_allowed");
    }

    [Fact]
    public void Procenta_mimo_rozsah_jsou_chyba()
    {
        Should.Throw<DomainException>(() => new CancellationPolicy(true, 24, 120m, 0m))
            .Code.ShouldBe("cancellation_policy.invalid_percent");
    }

    [Fact]
    public void Vychozi_politika_je_storno_zdarma_do_24_hodin()
    {
        CancellationPolicy.Default.FreeCancellationHours.ShouldBe(24);
        CancellationPolicy.Default.LateCancellationRefundPercent.ShouldBe(0m);
        CancellationPolicy.Default.CancellationAllowed.ShouldBeTrue();
    }
}
