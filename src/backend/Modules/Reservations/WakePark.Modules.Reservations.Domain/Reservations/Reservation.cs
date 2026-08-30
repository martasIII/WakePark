using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Reservations.Domain.Reservations;

/// <summary>
/// Rezervace míst v jednom slotu. Jedna rezervace může držet víc míst (rodina na jeden doklad),
/// každé místo odpovídá jedné jízdě.
/// </summary>
public sealed class Reservation : AggregateRoot, IAuditable
{
    private Reservation()
    {
    }

    private Reservation(
        Guid id,
        Guid slotId,
        Guid customerId,
        int seats,
        ReservationSource source,
        string? idempotencyKey)
        : base(id)
    {
        SlotId = slotId;
        CustomerId = customerId;
        Seats = seats;
        Source = source;
        IdempotencyKey = idempotencyKey;
        Status = ReservationStatus.Pending;
    }

    public Guid SlotId { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>Doklad. Null, dokud nebyl vystaven.</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>Permanentka, ze které se čerpalo. Null u platby v hotovosti nebo kartou.</summary>
    public Guid? RidePassId { get; private set; }

    /// <summary>Kolik míst ve slotu rezervace drží.</summary>
    public int Seats { get; private set; }

    public ReservationStatus Status { get; private set; }

    public ReservationSource Source { get; private set; }

    /// <summary>
    /// Klíč pro ochranu proti dvojímu odeslání. Opakovaný požadavek se stejným klíčem
    /// vrátí původní rezervaci místo vytvoření nové.
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    /// <summary>Kolik procent bylo při stornu vráceno. Vyplní se spolu se stornem.</summary>
    public decimal? RefundPercent { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Reservation Create(
        Guid slotId,
        Guid customerId,
        int seats,
        ReservationSource source,
        string? idempotencyKey = null)
    {
        if (slotId == Guid.Empty)
        {
            throw new DomainException("reservation.slot_required", "Rezervace musí mít slot.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("reservation.customer_required", "Rezervace musí mít zákazníka.");
        }

        if (seats <= 0)
        {
            throw new DomainException("reservation.invalid_seats", "Počet míst musí být kladný.");
        }

        return new Reservation(NewId(), slotId, customerId, seats, source, idempotencyKey);
    }

    public void ConfirmPaidByPass(Guid ridePassId)
    {
        if (ridePassId == Guid.Empty)
        {
            throw new DomainException("reservation.pass_required", "Chybí permanentka, ze které se čerpá.");
        }

        RidePassId = ridePassId;
        Confirm();
    }

    public void Confirm()
    {
        if (Status is not ReservationStatus.Pending)
        {
            throw new DomainException(
                "reservation.not_pending",
                "Potvrdit lze jen rezervaci, která čeká na potvrzení.");
        }

        Status = ReservationStatus.Confirmed;
    }

    public void AttachOrder(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("reservation.order_required", "Doklad musí mít identifikátor.");
        }

        OrderId = orderId;
    }

    public void CheckIn()
    {
        if (Status is not ReservationStatus.Confirmed)
        {
            throw new DomainException("reservation.not_confirmed", "Odbavit lze jen potvrzenou rezervaci.");
        }

        Status = ReservationStatus.CheckedIn;
    }

    public void Complete()
    {
        if (Status is not (ReservationStatus.Confirmed or ReservationStatus.CheckedIn))
        {
            throw new DomainException("reservation.not_ridable", "Dokončit lze jen aktivní rezervaci.");
        }

        Status = ReservationStatus.Completed;
    }

    /// <summary>Stornuje rezervaci. Kolik se vrátí, určuje politika areálu — sem přichází výsledek.</summary>
    public void Cancel(DateTimeOffset cancelledAtUtc, string reason, decimal refundPercent)
    {
        if (Status is ReservationStatus.Cancelled)
        {
            throw new DomainException("reservation.already_cancelled", "Rezervace už je stornovaná.");
        }

        if (Status is ReservationStatus.Completed)
        {
            throw new DomainException("reservation.already_completed", "Odjetou rezervaci nelze stornovat.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Status = ReservationStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        CancellationReason = reason.Trim();
        RefundPercent = refundPercent;
    }

    public void MarkNoShow(decimal refundPercent)
    {
        if (Status is not (ReservationStatus.Confirmed or ReservationStatus.CheckedIn))
        {
            throw new DomainException("reservation.not_confirmed", "Nedostavení lze označit jen u potvrzené rezervace.");
        }

        Status = ReservationStatus.NoShow;
        RefundPercent = refundPercent;
    }

    /// <summary>Kolik jízd se má vrátit do kreditu při daném procentu. Zaokrouhluje dolů.</summary>
    public int RefundableRides() =>
        RefundPercent is { } percent ? (int)Math.Floor(Seats * percent / 100m) : 0;
}
