namespace WakePark.Modules.Reservations.Contracts;

/// <summary>Volná kapacita jednoho bloku.</summary>
/// <param name="StartUtc">Začátek bloku.</param>
/// <param name="EndUtc">Konec bloku, exkluzivně.</param>
/// <param name="Capacity">Kolik jezdců se v bloku vystřídá.</param>
/// <param name="ReservedCount">Kolik míst je obsazeno.</param>
/// <param name="IsOpen">Zda lze do bloku ještě rezervovat.</param>
public sealed record SlotAvailabilityDto(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int Capacity,
    int ReservedCount,
    bool IsOpen)
{
    /// <summary>Kolik míst zbývá.</summary>
    public int FreeCapacity => Capacity - ReservedCount;
}

/// <summary>Rezervace tak, jak ji vidí okolí.</summary>
public sealed record ReservationDto(
    Guid Id,
    Guid SlotId,
    Guid CustomerId,
    Guid? RidePassId,
    Guid? OrderId,
    DateTimeOffset SlotStartUtc,
    int Seats,
    string Status,
    string Source,
    DateTimeOffset? CancelledAtUtc,
    decimal? RefundPercent);
