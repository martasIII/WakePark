using WakePark.BuildingBlocks.Domain;

namespace WakePark.Modules.Operations.Domain.Scheduling;

/// <summary>
/// Jeden souvislý provozní blok v rámci dne, už bez vazby na sezónu a den v týdnu.
/// Mezivýsledek mezi provozním dnem (<see cref="Calendar.OperatingDay"/>) a výpočtem slotů.
/// </summary>
public sealed record DailySchedule
{
    public DailySchedule(TimeOnly start, TimeOnly end, int slotDurationMinutes, int capacity)
    {
        if (end <= start)
        {
            throw new DomainException("schedule.invalid_range", "Konec provozní doby musí být po jejím začátku.");
        }

        if (slotDurationMinutes <= 0)
        {
            throw new DomainException("schedule.invalid_duration", "Délka bloku musí být kladná.");
        }

        if (capacity <= 0)
        {
            throw new DomainException("schedule.invalid_capacity", "Kapacita bloku musí být kladná.");
        }

        Start = start;
        End = end;
        SlotDurationMinutes = slotDurationMinutes;
        Capacity = capacity;
    }

    public TimeOnly Start { get; }

    /// <summary>Konec provozní doby, exkluzivně.</summary>
    public TimeOnly End { get; }

    public int SlotDurationMinutes { get; }

    /// <summary>Kolik jezdců se v jednom bloku vystřídá.</summary>
    public int Capacity { get; }
}
