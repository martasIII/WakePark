using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Operations.Domain.Scheduling;

/// <summary>
/// Odchylka od pravidelné provozní doby v konkrétní den: bouřka, údržba, závody, svátek.
/// U wakeparku je to běžná věc, ne okrajový případ.
/// </summary>
public sealed class ScheduleOverride : AggregateRoot, IAuditable
{
    private ScheduleOverride()
    {
    }

    private ScheduleOverride(Guid id, Guid cableId, DateOnly date, bool isClosed, string reason)
        : base(id)
    {
        CableId = cableId;
        Date = date;
        IsClosed = isClosed;
        Reason = reason;
    }

    public Guid CableId { get; private set; }

    /// <summary>Datum v lokálním čase areálu.</summary>
    public DateOnly Date { get; private set; }

    public bool IsClosed { get; private set; }

    public TimeOnly? CustomStartTime { get; private set; }

    public TimeOnly? CustomEndTime { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>Celodenní uzavírka.</summary>
    public static ScheduleOverride Closed(Guid cableId, DateOnly date, string reason)
    {
        EnsureCable(cableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new ScheduleOverride(NewId(), cableId, date, isClosed: true, reason.Trim());
    }

    /// <summary>Zkrácený nebo posunutý provoz.</summary>
    public static ScheduleOverride CustomHours(
        Guid cableId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        string reason)
    {
        EnsureCable(cableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (endTime <= startTime)
        {
            throw new DomainException(
                "operating_exception.invalid_range",
                "Konec mimořádné provozní doby musí být po jejím začátku.");
        }

        return new ScheduleOverride(NewId(), cableId, date, isClosed: false, reason.Trim())
        {
            CustomStartTime = startTime,
            CustomEndTime = endTime,
        };
    }

    /// <summary>
    /// Promítne výjimku do provozní doby dne. Zavřeno = prázdný výsledek, jinak se blokům
    /// ořežou hranice na mimořádnou dobu; bloky, které se do ní nevejdou, vypadnou.
    /// </summary>
    public IReadOnlyList<DailySchedule> ApplyTo(IReadOnlyList<DailySchedule> schedules)
    {
        ArgumentNullException.ThrowIfNull(schedules);

        if (IsClosed)
        {
            return [];
        }

        if (CustomStartTime is not { } customStart || CustomEndTime is not { } customEnd)
        {
            return schedules;
        }

        List<DailySchedule> result = [];

        foreach (DailySchedule schedule in schedules)
        {
            TimeOnly start = schedule.Start > customStart ? schedule.Start : customStart;
            TimeOnly end = schedule.End < customEnd ? schedule.End : customEnd;

            if (end > start)
            {
                result.Add(schedule.WithHours(start, end));
            }
        }

        return result;
    }

    private static void EnsureCable(Guid cableId)
    {
        if (cableId == Guid.Empty)
        {
            throw new DomainException("operating_exception.cable_required", "Výjimka musí patřit vleku.");
        }
    }
}
