using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Operations.Domain.Scheduling;

/// <summary>
/// Provozní doba vleku pro jeden den v týdnu v rámci sezóny. Časy jsou v <b>lokálním čase
/// areálu</b> — na okamžiky se převádějí až při plánování slotů, protože jinak by se rozešly
/// při přechodu na letní čas.
/// </summary>
public sealed class WorkingHours : AggregateRoot, IAuditable
{
    private WorkingHours()
    {
    }

    private WorkingHours(
        Guid id,
        Guid cableId,
        Guid seasonId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotDurationMinutes,
        int capacity)
        : base(id)
    {
        CableId = cableId;
        SeasonId = seasonId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        SlotDurationMinutes = slotDurationMinutes;
        Capacity = capacity;
    }

    public Guid CableId { get; private set; }

    public Guid SeasonId { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly StartTime { get; private set; }

    /// <summary>Konec provozní doby, exkluzivně.</summary>
    public TimeOnly EndTime { get; private set; }

    /// <summary>Délka jednoho bloku. Určuje, na kolik slotů se provozní doba rozpadne.</summary>
    public int SlotDurationMinutes { get; private set; }

    /// <summary>Kolik jezdců si blok sdílí a střídá se v něm.</summary>
    public int Capacity { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static WorkingHours Create(
        Guid cableId,
        Guid seasonId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotDurationMinutes,
        int capacity)
    {
        if (cableId == Guid.Empty)
        {
            throw new DomainException("working_hours.cable_required", "Provozní doba musí patřit vleku.");
        }

        if (seasonId == Guid.Empty)
        {
            throw new DomainException("working_hours.season_required", "Provozní doba musí patřit sezóně.");
        }

        // Ověření rozsahu, délky a kapacity je v DailySchedule — ať platí jen jedna sada pravidel.
        _ = new DailySchedule(startTime, endTime, slotDurationMinutes, capacity);

        return new WorkingHours(
            NewId(), cableId, seasonId, dayOfWeek, startTime, endTime, slotDurationMinutes, capacity);
    }

    public DailySchedule ToDailySchedule() =>
        new(StartTime, EndTime, SlotDurationMinutes, Capacity);
}
