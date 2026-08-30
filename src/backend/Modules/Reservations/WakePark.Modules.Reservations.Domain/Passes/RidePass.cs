using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Reservations.Domain.Passes;

/// <summary>
/// Permanentka zákazníka. Zůstatek <b>není sloupec</b> — počítá se jako součet pohybů
/// (<see cref="CreditTransaction"/>). Přepisovaný zůstatek by se dřív nebo později rozešel
/// s historií a nešlo by dohledat proč.
/// </summary>
public sealed class RidePass : AggregateRoot, IAuditable
{
    private readonly List<CreditTransaction> _transactions = [];

    private RidePass()
    {
    }

    private RidePass(
        Guid id,
        Guid customerId,
        Guid wakeParkId,
        RidePassKind kind,
        DateOnly validFrom,
        DateOnly validTo,
        bool isTransferable)
        : base(id)
    {
        CustomerId = customerId;
        WakeParkId = wakeParkId;
        Kind = kind;
        ValidFrom = validFrom;
        ValidTo = validTo;
        IsTransferable = isTransferable;
        Status = RidePassStatus.Active;
    }

    public Guid CustomerId { get; private set; }

    public Guid WakeParkId { get; private set; }

    public RidePassKind Kind { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    /// <summary>Poslední den platnosti, včetně.</summary>
    public DateOnly ValidTo { get; private set; }

    public bool IsTransferable { get; private set; }

    public RidePassStatus Status { get; private set; }

    public IReadOnlyCollection<CreditTransaction> Transactions => _transactions.AsReadOnly();

    /// <summary>Zůstatek v jízdách. U sezónní permanentky nemá význam a je vždy 0.</summary>
    public int Balance => _transactions.Sum(transaction => transaction.Delta);

    /// <summary>Sezónní permanentka kredit nečerpá — platí neomezeně v období platnosti.</summary>
    public bool ConsumesCredit => Kind is RidePassKind.RideCredit;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static RidePass Create(
        Guid customerId,
        Guid wakeParkId,
        RidePassKind kind,
        DateOnly validFrom,
        DateOnly validTo,
        bool isTransferable = false)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("ride_pass.customer_required", "Permanentka musí mít zákazníka.");
        }

        if (wakeParkId == Guid.Empty)
        {
            throw new DomainException("ride_pass.park_required", "Permanentka musí patřit areálu.");
        }

        if (validTo < validFrom)
        {
            throw new DomainException("ride_pass.invalid_validity", "Konec platnosti nesmí předcházet jejímu začátku.");
        }

        return new RidePass(NewId(), customerId, wakeParkId, kind, validFrom, validTo, isTransferable);
    }

    public bool IsValidOn(DateOnly date) =>
        Status is RidePassStatus.Active && date >= ValidFrom && date <= ValidTo;

    /// <summary>Nabije kredit.</summary>
    public void TopUp(int rides, DateTimeOffset occurredAtUtc, string? note = null)
    {
        EnsureRideCredit();
        EnsurePositive(rides);

        _transactions.Add(new CreditTransaction(
            Id, rides, CreditReason.Purchase, occurredAtUtc, reservationId: null, note));
    }

    /// <summary>Odečte kredit za rezervaci.</summary>
    public void Consume(int rides, Guid reservationId, DateTimeOffset occurredAtUtc)
    {
        EnsurePositive(rides);

        if (Status is not RidePassStatus.Active)
        {
            throw new DomainException("ride_pass.not_active", "Permanentka není aktivní.");
        }

        if (!ConsumesCredit)
        {
            // Sezónní permanentka se nečerpá, jen se ověřuje platnost.
            return;
        }

        if (Balance < rides)
        {
            throw new DomainException(
                "ride_pass.insufficient_credit",
                $"Na permanentce zbývá {Balance} jízd, požadováno {rides}.");
        }

        _transactions.Add(new CreditTransaction(
            Id, -rides, CreditReason.RideConsumed, occurredAtUtc, reservationId, note: null));
    }

    /// <summary>Vrátí kredit po stornu.</summary>
    public void Refund(int rides, Guid reservationId, DateTimeOffset occurredAtUtc, string? note = null)
    {
        if (!ConsumesCredit || rides == 0)
        {
            return;
        }

        EnsurePositive(rides);

        _transactions.Add(new CreditTransaction(
            Id, rides, CreditReason.Refund, occurredAtUtc, reservationId, note));
    }

    /// <summary>Ruční oprava obsluhou. Důvod je povinný — bez něj se to za rok nedohledá.</summary>
    public void Adjust(int delta, DateTimeOffset occurredAtUtc, string note)
    {
        EnsureRideCredit();
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        if (delta == 0)
        {
            throw new DomainException("ride_pass.zero_adjustment", "Nulová oprava nedává smysl.");
        }

        if (Balance + delta < 0)
        {
            throw new DomainException("ride_pass.negative_balance", "Oprava by uvedla zůstatek do minusu.");
        }

        _transactions.Add(new CreditTransaction(
            Id, delta, CreditReason.ManualAdjustment, occurredAtUtc, reservationId: null, note.Trim()));
    }

    public void Expire(DateTimeOffset occurredAtUtc)
    {
        if (Status is not RidePassStatus.Active)
        {
            return;
        }

        if (ConsumesCredit && Balance > 0)
        {
            _transactions.Add(new CreditTransaction(
                Id, -Balance, CreditReason.Expiry, occurredAtUtc, reservationId: null, "Vypršela platnost."));
        }

        Status = RidePassStatus.Expired;
    }

    public void Suspend() => Status = RidePassStatus.Suspended;

    private void EnsureRideCredit()
    {
        if (!ConsumesCredit)
        {
            throw new DomainException(
                "ride_pass.not_credit_based",
                "Sezónní permanentka kredit nevede.");
        }
    }

    private static void EnsurePositive(int rides)
    {
        if (rides <= 0)
        {
            throw new DomainException("ride_pass.invalid_rides", "Počet jízd musí být kladný.");
        }
    }
}
