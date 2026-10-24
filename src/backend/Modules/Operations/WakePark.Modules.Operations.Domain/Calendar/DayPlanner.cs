using WakePark.Modules.Operations.Domain.Parks;
using WakePark.Modules.Operations.Domain.Scheduling;

namespace WakePark.Modules.Operations.Domain.Calendar;

/// <summary>
/// Sloty vleku pro jeden den: provozní den areálu rozpadnutý na bloky podle nastavení, bez
/// bloků, které se překrývají s blokací vleku nebo celého areálu.
/// </summary>
/// <remarks>Čistá funkce bez databáze a systémového času — přechod na letní čas řeší <see cref="SlotPlanner"/>.</remarks>
public static class DayPlanner
{
    public static IReadOnlyList<SlotTemplate> Plan(
        Guid cableId,
        OperatingDay? day,
        ParkSettings settings,
        TimeZoneInfo timeZone,
        IReadOnlyCollection<Blocking> blockings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(blockings);

        // Den bez provozního dne = zavřeno. Není to chyba, jen prázdný rozvrh.
        if (day is null)
        {
            return [];
        }

        DailySchedule schedule = new(day.OpensAt, day.ClosesAt, settings.SlotDurationMinutes, settings.Capacity);
        Blocking[] relevant = [.. blockings.Where(b => b.AppliesTo(cableId))];

        return
        [
            .. SlotPlanner
                .Plan(day.Date, timeZone, [schedule])
                .Where(slot => !relevant.Any(b => b.Overlaps(slot.StartUtc, slot.StartUtc.AddMinutes(slot.DurationMinutes)))),
        ];
    }
}
