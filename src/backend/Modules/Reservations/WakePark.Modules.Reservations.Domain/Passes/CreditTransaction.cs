using WakePark.BuildingBlocks.Domain;

namespace WakePark.Modules.Reservations.Domain.Passes;

/// <summary>
/// Jeden pohyb kreditu permanentky. Řádky jsou <b>append-only</b> — nikdy se needitují ani
/// nemažou. Oprava se dělá novou opačnou transakcí, jinak by nešlo dohledat, jak zůstatek vznikl.
/// </summary>
public sealed class CreditTransaction : Entity
{
    private CreditTransaction()
    {
    }

    internal CreditTransaction(
        Guid ridePassId,
        int delta,
        CreditReason reason,
        DateTimeOffset occurredAtUtc,
        Guid? reservationId,
        string? note)
        : base(NewId())
    {
        RidePassId = ridePassId;
        Delta = delta;
        Reason = reason;
        OccurredAtUtc = occurredAtUtc;
        ReservationId = reservationId;
        Note = note;
    }

    public Guid RidePassId { get; private set; }

    /// <summary>Změna zůstatku v jízdách. Kladné = nabití, záporné = čerpání.</summary>
    public int Delta { get; private set; }

    public CreditReason Reason { get; private set; }

    public Guid? ReservationId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? Note { get; private set; }
}
