using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Calendar;
using WakePark.Modules.Operations.Domain.Parks;
using WakePark.Modules.Operations.Domain.Scheduling;

namespace WakePark.Modules.Operations.UnitTests.Calendar;

public sealed class CalendarDomainTests
{
    private static readonly TimeZoneInfo Prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
    private static readonly Guid ParkId = Guid.CreateVersion7();
    private static readonly Guid Cable1 = Guid.CreateVersion7();
    private static readonly Guid Cable2 = Guid.CreateVersion7();
    private static readonly DateOnly Day = new(2027, 7, 15);

    private readonly ParkSettings _settings = ParkSettings.Create(ParkId, 20, 3, 14, 30);

    [Fact]
    public void Den_bez_provozniho_dne_je_zavreno()
    {
        DayPlanner.Plan(Cable1, null, _settings, Prague, []).ShouldBeEmpty();
    }

    [Fact]
    public void Provozni_den_se_rozpadne_na_dvacetiminutove_bloky_pro_tri_jezdce()
    {
        OperatingDay day = OperatingDay.Create(ParkId, Day, new TimeOnly(10, 0), new TimeOnly(11, 0));

        IReadOnlyList<SlotTemplate> slots = DayPlanner.Plan(Cable1, day, _settings, Prague, []);

        slots.Count.ShouldBe(3);
        slots.ShouldAllBe(s => s.DurationMinutes == 20 && s.Capacity == 3);
        slots[0].StartUtc.ShouldBe(new DateTimeOffset(2027, 7, 15, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Blokace_vleku_vyradi_jen_jeho_zasazene_bloky_blokace_arealu_vsechny()
    {
        OperatingDay day = OperatingDay.Create(ParkId, Day, new TimeOnly(10, 0), new TimeOnly(11, 0));
        // 10:30–10:50 lokálně = 8:30–8:50 UTC; zasáhne bloky 10:20 a 10:40.
        Blocking cableFault = Blocking.Create(ParkId, Cable1, Utc(8, 30), Utc(8, 50), BlockingCategory.TechnicalFault, "Prasklé lano");
        Blocking storm = Blocking.Create(ParkId, null, Utc(8, 0), Utc(8, 20), BlockingCategory.Weather, null);

        DayPlanner.Plan(Cable1, day, _settings, Prague, [cableFault]).Select(s => s.StartUtc).ShouldBe([Utc(8, 0)]);
        DayPlanner.Plan(Cable2, day, _settings, Prague, [cableFault]).Count.ShouldBe(3);
        DayPlanner.Plan(Cable2, day, _settings, Prague, [storm]).Select(s => s.StartUtc).ShouldBe([Utc(8, 20), Utc(8, 40)]);
    }

    [Fact]
    public void Import_ukaze_nove_zmenene_zavrene_a_beze_zmeny_dny()
    {
        OperatingDay may1 = OperatingDay.Create(ParkId, new DateOnly(2027, 5, 1), new TimeOnly(10, 0), new TimeOnly(19, 0));
        OperatingDay may2 = OperatingDay.Create(ParkId, new DateOnly(2027, 5, 2), new TimeOnly(10, 0), new TimeOnly(19, 0));
        OperatingDay may3 = OperatingDay.Create(ParkId, new DateOnly(2027, 5, 3), new TimeOnly(10, 0), new TimeOnly(19, 0));

        IReadOnlyList<OperatingDayDiff> plan = OperatingDaysImport.Plan(
            [may1, may2, may3],
            [
                new CsvOperatingDay(new DateOnly(2027, 5, 1), new TimeOnly(10, 0), new TimeOnly(19, 0)),
                new CsvOperatingDay(new DateOnly(2027, 5, 2), new TimeOnly(9, 0), new TimeOnly(20, 0)),
                new CsvOperatingDay(new DateOnly(2027, 5, 3), null, null),
                new CsvOperatingDay(new DateOnly(2027, 5, 4), new TimeOnly(10, 0), new TimeOnly(19, 0)),
                new CsvOperatingDay(new DateOnly(2027, 5, 5), null, null),
            ]);

        plan.Select(d => d.Change).ShouldBe(
        [
            OperatingDayChange.Unchanged,
            OperatingDayChange.Changed,
            OperatingDayChange.Closed,
            OperatingDayChange.Added,
            OperatingDayChange.Unchanged,
        ]);
        plan[1].OldOpensAt.ShouldBe(new TimeOnly(10, 0));
        plan[1].NewOpensAt.ShouldBe(new TimeOnly(9, 0));
    }

    [Fact]
    public void Blokace_musi_mit_konec_po_zacatku_a_nejvys_mesic()
    {
        Should.Throw<DomainException>(() => Blocking.Create(ParkId, null, Utc(10, 0), Utc(10, 0), BlockingCategory.Other, null));
        Should.Throw<DomainException>(() => Blocking.Create(ParkId, null, Utc(10, 0), Utc(10, 0).AddDays(32), BlockingCategory.Other, null));
    }

    [Fact]
    public void Zavirani_pred_otevrenim_neprojde()
    {
        Should.Throw<DomainException>(() => OperatingDay.Create(ParkId, Day, new TimeOnly(19, 0), new TimeOnly(10, 0)))
            .Code.ShouldBe("operating_day.invalid_hours");
    }

    [Fact]
    public void Vlek_ma_kod_parametry_a_prekazky_na_draze()
    {
        Cable cable = Cable.Create(ParkId, "vlek-1", "Vlek 1", 2);

        cable.Update("Vlek 1", 2, 180, "Delší dráha", [new Obstacle("Kicker", "kicker", 40, null), new Obstacle("Rail", "rail", 120, "Dlouhý")]);

        cable.Obstacles.Select(o => o.Name).ShouldBe(["Kicker", "Rail"]);
        Should.Throw<DomainException>(() => cable.Update("Vlek 1", 2, 100, null, [new Obstacle("Rail", "rail", 120, null)]))
            .Code.ShouldBe("cable.obstacle_outside");
        Should.Throw<DomainException>(() => Cable.Create(ParkId, "Vlek 1", "Vlek 1", 2)).Code.ShouldBe("cable.invalid_code");
    }

    private static DateTimeOffset Utc(int hour, int minute) => new(2027, 7, 15, hour, minute, 0, TimeSpan.Zero);
}
