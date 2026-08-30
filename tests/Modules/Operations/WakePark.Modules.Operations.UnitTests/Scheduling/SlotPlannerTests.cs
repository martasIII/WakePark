using Shouldly;

using WakePark.Modules.Operations.Domain.Scheduling;

namespace WakePark.Modules.Operations.UnitTests.Scheduling;

/// <summary>
/// Přechod na letní čas je jediné místo, kde se plánování slotů dá snadno rozbít.
/// Proto je otestovaný explicitně, ne jen "nějak to funguje".
/// </summary>
public sealed class SlotPlannerTests
{
    private static readonly TimeZoneInfo Prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

    [Fact]
    public void Bezny_den_se_rozpadne_na_bloky_dane_delky()
    {
        DailySchedule schedule = new(new TimeOnly(9, 0), new TimeOnly(12, 0), slotDurationMinutes: 60, capacity: 3);

        IReadOnlyList<SlotTemplate> slots = SlotPlanner.Plan(new DateOnly(2026, 7, 15), Prague, [schedule]);

        slots.Count.ShouldBe(3);
        slots[0].StartUtc.ShouldBe(new DateTimeOffset(2026, 7, 15, 7, 0, 0, TimeSpan.Zero));
        slots[2].StartUtc.ShouldBe(new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Neuplny_zbytek_provozni_doby_slot_nevytvori()
    {
        // 9:00–10:30 při 60minutových blocích dá jen jeden celý blok, půlhodina se zahodí.
        DailySchedule schedule = new(new TimeOnly(9, 0), new TimeOnly(10, 30), slotDurationMinutes: 60, capacity: 1);

        IReadOnlyList<SlotTemplate> slots = SlotPlanner.Plan(new DateOnly(2026, 7, 15), Prague, [schedule]);

        slots.Count.ShouldBe(1);
    }

    [Fact]
    public void Jarni_posun_vynecha_hodinu_ktera_neexistuje()
    {
        // 29. 3. 2026 se ve 2:00 přeskakuje na 3:00 — blok začínající ve 2:00 v realitě neproběhne.
        DailySchedule schedule = new(new TimeOnly(1, 0), new TimeOnly(5, 0), slotDurationMinutes: 60, capacity: 1);

        IReadOnlyList<SlotTemplate> slots = SlotPlanner.Plan(new DateOnly(2026, 3, 29), Prague, [schedule]);

        slots.Count.ShouldBe(3);
        slots.ShouldAllBe(slot => slot.StartUtc.Offset == TimeSpan.Zero);
    }

    [Fact]
    public void Podzimni_posun_vytvori_dvojznacnou_hodinu_jen_jednou()
    {
        // 25. 10. 2026 nastane 2:00 dvakrát. Bereme první (letní) výskyt, tedy UTC půlnoc.
        DailySchedule schedule = new(new TimeOnly(1, 0), new TimeOnly(5, 0), slotDurationMinutes: 60, capacity: 1);

        IReadOnlyList<SlotTemplate> slots = SlotPlanner.Plan(new DateOnly(2026, 10, 25), Prague, [schedule]);

        slots.Count.ShouldBe(4);
        slots[1].StartUtc.ShouldBe(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Sloty_se_vraci_serazene_i_pri_vice_provoznich_blocich()
    {
        DailySchedule odpoledne = new(new TimeOnly(14, 0), new TimeOnly(16, 0), 60, 3);
        DailySchedule dopoledne = new(new TimeOnly(9, 0), new TimeOnly(11, 0), 60, 3);

        IReadOnlyList<SlotTemplate> slots =
            SlotPlanner.Plan(new DateOnly(2026, 7, 15), Prague, [odpoledne, dopoledne]);

        slots.Count.ShouldBe(4);
        slots.ShouldBeInOrder(SortDirection.Ascending, Comparer<SlotTemplate>.Create(
            (left, right) => left.StartUtc.CompareTo(right.StartUtc)));
    }
}
