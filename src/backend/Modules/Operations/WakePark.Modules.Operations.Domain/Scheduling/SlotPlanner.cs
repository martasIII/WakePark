namespace WakePark.Modules.Operations.Domain.Scheduling;

/// <summary>
/// Převádí provozní dobu (lokální čas) na sloty (okamžiky v UTC).
/// </summary>
/// <remarks>
/// Tohle je jediné místo, kde se řeší přechod na letní čas, a je proto záměrně čisté —
/// bez databáze a bez systémového času, aby šlo otestovat každý hraniční případ.
///
/// Dvě situace, které se v Evropě dějí dvakrát ročně:
/// <list type="bullet">
///   <item>
///     <b>Jarní posun</b> — lokální čas 2:00–3:00 neexistuje. Sloty v této době se vynechají,
///     protože v realitě neproběhnou.
///   </item>
///   <item>
///     <b>Podzimní posun</b> — lokální čas 2:00–3:00 nastane dvakrát. Bereme <b>první</b>
///     (letní) výskyt: park zavírá dřív, než hodina proběhne podruhé, a dva sloty se stejným
///     lokálním časem by uživatele jen zmátly.
///   </item>
/// </list>
/// </remarks>
public static class SlotPlanner
{
    /// <summary>Rozpadne provozní dobu jednoho dne na jednotlivé sloty.</summary>
    /// <param name="localDate">Den v lokálním čase areálu.</param>
    /// <param name="timeZone">Zóna areálu.</param>
    /// <param name="schedules">Provozní bloky toho dne, už po promítnutí výjimek.</param>
    public static IReadOnlyList<SlotTemplate> Plan(
        DateOnly localDate,
        TimeZoneInfo timeZone,
        IReadOnlyList<DailySchedule> schedules)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(schedules);

        List<SlotTemplate> slots = [];

        foreach (DailySchedule schedule in schedules)
        {
            TimeSpan step = TimeSpan.FromMinutes(schedule.SlotDurationMinutes);
            DateTime localStart = localDate.ToDateTime(schedule.Start, DateTimeKind.Unspecified);
            DateTime localEnd = localDate.ToDateTime(schedule.End, DateTimeKind.Unspecified);

            for (DateTime cursor = localStart; cursor + step <= localEnd; cursor += step)
            {
                if (TryToUtc(cursor, timeZone, out DateTimeOffset startUtc))
                {
                    slots.Add(new SlotTemplate(startUtc, schedule.SlotDurationMinutes, schedule.Capacity));
                }
            }
        }

        return slots
            .OrderBy(slot => slot.StartUtc)
            .ToList();
    }

    /// <summary>
    /// Převede lokální čas na okamžik. Vrací <c>false</c> pro čas, který kvůli jarnímu posunu
    /// neexistuje.
    /// </summary>
    public static bool TryToUtc(DateTime localTime, TimeZoneInfo timeZone, out DateTimeOffset utc)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        if (timeZone.IsInvalidTime(localTime))
        {
            utc = default;
            return false;
        }

        if (timeZone.IsAmbiguousTime(localTime))
        {
            // Ze dvou možných posunů bereme ten první v čase, tedy větší (letní).
            TimeSpan[] offsets = timeZone.GetAmbiguousTimeOffsets(localTime);
            TimeSpan earliest = offsets.Max();
            utc = new DateTimeOffset(localTime, earliest).ToUniversalTime();
            return true;
        }

        TimeSpan offset = timeZone.GetUtcOffset(localTime);
        utc = new DateTimeOffset(localTime, offset).ToUniversalTime();
        return true;
    }
}
