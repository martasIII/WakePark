using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Reservations.Domain.Passes;

namespace WakePark.Modules.Reservations.UnitTests.Domain;

public sealed class RidePassTests
{
    private static readonly Guid CustomerId = Guid.CreateVersion7();

    private static readonly Guid ParkId = Guid.CreateVersion7();

    private static readonly Guid ReservationId = Guid.CreateVersion7();

    private static readonly DateTimeOffset Now = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    private static RidePass CreditPass() => RidePass.Create(
        CustomerId, ParkId, RidePassKind.RideCredit, new DateOnly(2026, 5, 1), new DateOnly(2026, 9, 30));

    [Fact]
    public void Zustatek_je_soucet_pohybu()
    {
        RidePass pass = CreditPass();

        pass.TopUp(10, Now);
        pass.Consume(3, ReservationId, Now);

        pass.Balance.ShouldBe(7);
        pass.Transactions.Count.ShouldBe(2);
    }

    [Fact]
    public void Cerpani_nad_zustatek_je_domenova_chyba()
    {
        RidePass pass = CreditPass();
        pass.TopUp(2, Now);

        Should.Throw<DomainException>(() => pass.Consume(3, ReservationId, Now))
            .Code.ShouldBe("ride_pass.insufficient_credit");
    }

    [Fact]
    public void Storno_vrati_kredit_novym_pohybem_ne_upravou_puvodniho()
    {
        RidePass pass = CreditPass();
        pass.TopUp(5, Now);
        pass.Consume(2, ReservationId, Now);

        pass.Refund(2, ReservationId, Now, "Storno v termínu");

        pass.Balance.ShouldBe(5);
        pass.Transactions.Count.ShouldBe(3);
        pass.Transactions.Count(t => t.Reason == CreditReason.Refund).ShouldBe(1);
    }

    [Fact]
    public void Sezonni_permanentka_kredit_necerpa()
    {
        RidePass pass = RidePass.Create(
            CustomerId, ParkId, RidePassKind.SeasonPass, new DateOnly(2026, 5, 1), new DateOnly(2026, 9, 30));

        pass.Consume(3, ReservationId, Now);

        pass.Balance.ShouldBe(0);
        pass.Transactions.ShouldBeEmpty();
    }

    [Fact]
    public void Rucni_oprava_nesmi_dostat_zustatek_do_minusu()
    {
        RidePass pass = CreditPass();
        pass.TopUp(3, Now);

        Should.Throw<DomainException>(() => pass.Adjust(-5, Now, "Překlep obsluhy"))
            .Code.ShouldBe("ride_pass.negative_balance");
    }

    [Fact]
    public void Vyprseni_platnosti_zustatek_vynuluje_zaznamem_ne_smazanim()
    {
        RidePass pass = CreditPass();
        pass.TopUp(4, Now);

        pass.Expire(Now);

        pass.Balance.ShouldBe(0);
        pass.Status.ShouldBe(RidePassStatus.Expired);
        pass.Transactions.Count(t => t.Reason == CreditReason.Expiry).ShouldBe(1);
    }

    [Fact]
    public void Platnost_se_posuzuje_vcetne_krajnich_dnu()
    {
        RidePass pass = CreditPass();

        pass.IsValidOn(new DateOnly(2026, 5, 1)).ShouldBeTrue();
        pass.IsValidOn(new DateOnly(2026, 9, 30)).ShouldBeTrue();
        pass.IsValidOn(new DateOnly(2026, 10, 1)).ShouldBeFalse();
    }
}
