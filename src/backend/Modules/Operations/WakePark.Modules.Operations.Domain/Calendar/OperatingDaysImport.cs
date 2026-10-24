namespace WakePark.Modules.Operations.Domain.Calendar;

/// <summary>Co import udělá s jedním dnem.</summary>
public enum OperatingDayChange
{
    /// <summary>Den byl zavřený, bude otevřený.</summary>
    Added,

    /// <summary>Jiná otevírací doba.</summary>
    Changed,

    /// <summary>Byl otevřený, v CSV je zavřený — záznam se smaže.</summary>
    Closed,

    Unchanged,
}

public sealed record OperatingDayDiff(
    DateOnly Date,
    OperatingDayChange Change,
    TimeOnly? OldOpensAt,
    TimeOnly? OldClosesAt,
    TimeOnly? NewOpensAt,
    TimeOnly? NewClosesAt);

/// <summary>
/// Náhled importu: porovná CSV s provozními dny v databázi (ADR 0009 — import nejdřív ukáže,
/// co se změní, a uloží se až po potvrzení). Dny, které v CSV nejsou, zůstanou, jak jsou —
/// CSV smí pokrývat jen část sezóny.
/// </summary>
public static class OperatingDaysImport
{
    public static IReadOnlyList<OperatingDayDiff> Plan(
        IReadOnlyCollection<OperatingDay> existing,
        IReadOnlyList<CsvOperatingDay> imported)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(imported);

        Dictionary<DateOnly, OperatingDay> byDate = existing.ToDictionary(d => d.Date);
        List<OperatingDayDiff> plan = [];

        foreach (CsvOperatingDay day in imported)
        {
            byDate.TryGetValue(day.Date, out OperatingDay? current);

            OperatingDayChange change = (current, day.IsClosed) switch
            {
                (null, true) => OperatingDayChange.Unchanged,
                (null, false) => OperatingDayChange.Added,
                (_, true) => OperatingDayChange.Closed,
                _ when current.OpensAt == day.OpensAt && current.ClosesAt == day.ClosesAt => OperatingDayChange.Unchanged,
                _ => OperatingDayChange.Changed,
            };

            plan.Add(new OperatingDayDiff(day.Date, change, current?.OpensAt, current?.ClosesAt, day.OpensAt, day.ClosesAt));
        }

        return plan;
    }
}
