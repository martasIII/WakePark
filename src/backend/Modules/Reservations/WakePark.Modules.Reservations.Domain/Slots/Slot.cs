using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Reservations.Domain.Slots;

/// <summary>
/// Rezervovatelný blok na vleku. Vzniká <b>lazy</b> — řádek se založí teprve s první rezervací
/// v daném čase, kalendář se dopředu negeneruje.
/// </summary>
/// <remarks>
/// Kapacita není počet lan: na laně je vždy jeden jezdec. <see cref="Capacity"/> říká, kolik
/// jezdců si blok sdílí a střídá se v něm.
///
/// Délka i kapacita se do slotu <b>zamrazují</b> z provozní doby v okamžiku vzniku. Pozdější
/// změna rozvrhu tak nikdy zpětně nezneplatní už přijaté rezervace.
/// </remarks>
public sealed class Slot : AggregateRoot, IAuditable
{
    private Slot()
    {
    }

    private Slot(Guid id, Guid cableId, DateTimeOffset startUtc, int durationMinutes, int capacity)
        : base(id)
    {
        CableId = cableId;
        StartUtc = startUtc;
        DurationMinutes = durationMinutes;
        Capacity = capacity;
        ReservedCount = 0;
        Status = SlotStatus.Open;
    }

    public Guid CableId { get; private set; }

    public DateTimeOffset StartUtc { get; private set; }

    public int DurationMinutes { get; private set; }

    /// <summary>Kolik jezdců se v bloku vystřídá. Zamrazeno z provozní doby.</summary>
    public int Capacity { get; private set; }

    /// <summary>Kolik míst je už obsazeno. Nikdy nesmí překročit <see cref="Capacity"/>.</summary>
    public int ReservedCount { get; private set; }

    public SlotStatus Status { get; private set; }

    public DateTimeOffset EndUtc => StartUtc.AddMinutes(DurationMinutes);

    public int FreeCapacity => Capacity - ReservedCount;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>Založí slot podle předpisu z rozvrhu.</summary>
    public static Slot Materialize(Guid cableId, DateTimeOffset startUtc, int durationMinutes, int capacity)
    {
        if (cableId == Guid.Empty)
        {
            throw new DomainException("slot.cable_required", "Slot musí patřit vleku.");
        }

        if (durationMinutes <= 0)
        {
            throw new DomainException("slot.invalid_duration", "Délka slotu musí být kladná.");
        }

        if (capacity <= 0)
        {
            throw new DomainException("slot.invalid_capacity", "Kapacita slotu musí být kladná.");
        }

        return new Slot(NewId(), cableId, startUtc.ToUniversalTime(), durationMinutes, capacity);
    }

    /// <summary>Obsadí <paramref name="seats"/> míst.</summary>
    public void Reserve(int seats)
    {
        EnsurePositive(seats);

        if (Status is not SlotStatus.Open)
        {
            throw new DomainException("slot.not_open", "Do tohoto slotu už nelze rezervovat.");
        }

        if (ReservedCount + seats > Capacity)
        {
            throw new DomainException(
                "slot.capacity_exceeded",
                $"Ve slotu zbývá {FreeCapacity} míst, požadováno {seats}.");
        }

        ReservedCount += seats;
    }

    /// <summary>Uvolní <paramref name="seats"/> míst po stornu.</summary>
    public void Release(int seats)
    {
        EnsurePositive(seats);

        if (seats > ReservedCount)
        {
            throw new DomainException(
                "slot.release_exceeds_reserved",
                "Nelze uvolnit víc míst, než je obsazeno.");
        }

        ReservedCount -= seats;
    }

    public void Close() => Status = SlotStatus.Closed;

    public void Cancel() => Status = SlotStatus.Cancelled;

    private static void EnsurePositive(int seats)
    {
        if (seats <= 0)
        {
            throw new DomainException("slot.invalid_seats", "Počet míst musí být kladný.");
        }
    }
}
