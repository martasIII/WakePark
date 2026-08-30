using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Reservations.Domain.Reservations;

namespace WakePark.Modules.Reservations.UnitTests.Domain;

public sealed class ReservationTests
{
    private static readonly Guid SlotId = Guid.CreateVersion7();

    private static readonly Guid CustomerId = Guid.CreateVersion7();

    private static readonly DateTimeOffset Now = new(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);

    private static Reservation NewReservation(int seats = 2) =>
        Reservation.Create(SlotId, CustomerId, seats, ReservationSource.Online);

    [Fact]
    public void Nova_rezervace_ceka_na_potvrzeni()
    {
        NewReservation().Status.ShouldBe(ReservationStatus.Pending);
    }

    [Fact]
    public void Potvrzeni_permanentkou_ulozi_odkaz_na_ni()
    {
        Guid passId = Guid.CreateVersion7();
        Reservation reservation = NewReservation();

        reservation.ConfirmPaidByPass(passId);

        reservation.Status.ShouldBe(ReservationStatus.Confirmed);
        reservation.RidePassId.ShouldBe(passId);
    }

    [Fact]
    public void Odjetou_rezervaci_nelze_stornovat()
    {
        Reservation reservation = NewReservation();
        reservation.Confirm();
        reservation.Complete();

        Should.Throw<DomainException>(() => reservation.Cancel(Now, "Zákazník si to rozmyslel", 100m))
            .Code.ShouldBe("reservation.already_completed");
    }

    [Fact]
    public void Vracene_jizdy_se_pocitaji_z_procenta_a_zaokrouhluji_dolu()
    {
        Reservation reservation = NewReservation(seats: 3);
        reservation.Confirm();

        reservation.Cancel(Now, "Pozdní storno", 50m);

        // 3 místa × 50 % = 1,5 jízdy → vrací se 1.
        reservation.RefundableRides().ShouldBe(1);
    }

    [Fact]
    public void Vcasne_storno_vraci_vsechny_jizdy()
    {
        Reservation reservation = NewReservation(seats: 3);
        reservation.Confirm();

        reservation.Cancel(Now, "Storno v termínu", 100m);

        reservation.RefundableRides().ShouldBe(3);
    }

    [Fact]
    public void Rezervace_bez_mist_nedava_smysl()
    {
        Should.Throw<DomainException>(
            () => Reservation.Create(SlotId, CustomerId, 0, ReservationSource.Desk))
            .Code.ShouldBe("reservation.invalid_seats");
    }
}
