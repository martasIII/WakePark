using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Reservations.Domain.Slots;

namespace WakePark.Modules.Reservations.UnitTests.Domain;

public sealed class SlotTests
{
    private static readonly Guid CableId = Guid.CreateVersion7();

    private static readonly DateTimeOffset Start = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    private static Slot NewSlot(int capacity = 3) =>
        Slot.Materialize(CableId, Start, durationMinutes: 20, capacity);

    [Fact]
    public void Novy_slot_je_prazdny_a_otevreny()
    {
        Slot slot = NewSlot();

        slot.ReservedCount.ShouldBe(0);
        slot.FreeCapacity.ShouldBe(3);
        slot.Status.ShouldBe(SlotStatus.Open);
        slot.EndUtc.ShouldBe(Start.AddMinutes(20));
    }

    [Fact]
    public void Rezervace_snizi_volnou_kapacitu()
    {
        Slot slot = NewSlot();

        slot.Reserve(2);

        slot.ReservedCount.ShouldBe(2);
        slot.FreeCapacity.ShouldBe(1);
    }

    [Fact]
    public void Prekroceni_kapacity_je_domenova_chyba()
    {
        Slot slot = NewSlot(capacity: 2);
        slot.Reserve(2);

        DomainException exception = Should.Throw<DomainException>(() => slot.Reserve(1));

        exception.Code.ShouldBe("slot.capacity_exceeded");
        slot.ReservedCount.ShouldBe(2);
    }

    [Fact]
    public void Storno_vrati_misto_zpet()
    {
        Slot slot = NewSlot();
        slot.Reserve(3);

        slot.Release(1);

        slot.FreeCapacity.ShouldBe(1);
    }

    [Fact]
    public void Nelze_uvolnit_vic_mist_nez_je_obsazeno()
    {
        Slot slot = NewSlot();
        slot.Reserve(1);

        Should.Throw<DomainException>(() => slot.Release(2))
            .Code.ShouldBe("slot.release_exceeds_reserved");
    }

    [Fact]
    public void Do_uzavreneho_slotu_uz_nelze_rezervovat()
    {
        Slot slot = NewSlot();
        slot.Close();

        Should.Throw<DomainException>(() => slot.Reserve(1))
            .Code.ShouldBe("slot.not_open");
    }

    [Fact]
    public void Zacatek_se_vzdy_ulozi_v_UTC()
    {
        Slot slot = Slot.Materialize(
            CableId,
            new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(2)),
            20,
            1);

        slot.StartUtc.Offset.ShouldBe(TimeSpan.Zero);
        slot.StartUtc.Hour.ShouldBe(10);
    }
}
