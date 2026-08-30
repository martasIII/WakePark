using Shouldly;

using WakePark.Modules.Operations.Domain.Scheduling;

namespace WakePark.Modules.Operations.UnitTests.Scheduling;

public sealed class ScheduleOverrideTests
{
    private static readonly Guid CableId = Guid.CreateVersion7();

    private static readonly DateOnly Date = new(2026, 7, 15);

    private static DailySchedule FullDay() => new(new TimeOnly(9, 0), new TimeOnly(19, 0), 60, 3);

    [Fact]
    public void Uzavirka_zrusi_cely_den()
    {
        ScheduleOverride closure = ScheduleOverride.Closed(CableId, Date, "Bouřka");

        closure.ApplyTo([FullDay()]).ShouldBeEmpty();
    }

    [Fact]
    public void Zkraceny_provoz_orizne_hranice()
    {
        ScheduleOverride shortened = ScheduleOverride.CustomHours(
            CableId, Date, new TimeOnly(12, 0), new TimeOnly(16, 0), "Závody dopoledne");

        IReadOnlyList<DailySchedule> result = shortened.ApplyTo([FullDay()]);

        result.Count.ShouldBe(1);
        result[0].Start.ShouldBe(new TimeOnly(12, 0));
        result[0].End.ShouldBe(new TimeOnly(16, 0));
    }

    [Fact]
    public void Blok_mimo_mimoradnou_dobu_vypadne()
    {
        DailySchedule rano = new(new TimeOnly(6, 0), new TimeOnly(8, 0), 60, 1);

        ScheduleOverride afternoonOnly = ScheduleOverride.CustomHours(
            CableId, Date, new TimeOnly(12, 0), new TimeOnly(16, 0), "Údržba do poledne");

        afternoonOnly.ApplyTo([rano]).ShouldBeEmpty();
    }

    [Fact]
    public void Vyjimka_zachova_delku_bloku_i_kapacitu()
    {
        ScheduleOverride shortened = ScheduleOverride.CustomHours(
            CableId, Date, new TimeOnly(10, 0), new TimeOnly(18, 0), "Pozdější otevření");

        DailySchedule result = shortened.ApplyTo([FullDay()])[0];

        result.SlotDurationMinutes.ShouldBe(60);
        result.Capacity.ShouldBe(3);
    }
}
