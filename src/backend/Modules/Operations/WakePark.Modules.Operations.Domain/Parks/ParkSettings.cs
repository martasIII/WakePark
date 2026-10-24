using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Operations.Domain.Parks;

/// <summary>
/// Provozní nastavení areálu (ADR 0009). Délka bloku a kapacita jsou u Wake Parku celou sezónu
/// stejné, proto patří areálu, ne jednotlivým dnům.
/// </summary>
public sealed class ParkSettings : AggregateRoot, IAuditable
{
    private ParkSettings()
    {
    }

    private ParkSettings(Guid id, Guid wakeParkId)
        : base(id)
    {
        WakeParkId = wakeParkId;
    }

    public Guid WakeParkId { get; private set; }

    /// <summary>Délka jednoho bloku na laně (20 min).</summary>
    public int SlotDurationMinutes { get; private set; }

    /// <summary>Kolik jezdců se v bloku vystřídá (3).</summary>
    public int Capacity { get; private set; }

    /// <summary>Kolik dní dopředu ukazuje veřejný web otevírací dobu (14).</summary>
    public int PublicHoursDaysAhead { get; private set; }

    /// <summary>Jak daleko dopředu si zákazník může rezervovat online (30). Backoffice bez omezení.</summary>
    public int PublicBookingDaysAhead { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static ParkSettings Create(Guid wakeParkId, int slotDurationMinutes, int capacity, int publicHoursDaysAhead, int publicBookingDaysAhead)
    {
        if (wakeParkId == Guid.Empty)
        {
            throw new DomainException("park_settings.park_required", "Nastavení musí patřit areálu.");
        }

        ParkSettings settings = new(NewId(), wakeParkId);
        settings.Update(slotDurationMinutes, capacity, publicHoursDaysAhead, publicBookingDaysAhead);
        return settings;
    }

    public void Update(int slotDurationMinutes, int capacity, int publicHoursDaysAhead, int publicBookingDaysAhead)
    {
        if (slotDurationMinutes is < 5 or > 240)
        {
            throw new DomainException("park_settings.invalid_duration", "Délka bloku musí být 5 až 240 minut.");
        }

        if (capacity is < 1 or > 50)
        {
            throw new DomainException("park_settings.invalid_capacity", "Kapacita bloku musí být 1 až 50 jezdců.");
        }

        if (publicHoursDaysAhead is < 1 or > 366 || publicBookingDaysAhead is < 1 or > 366)
        {
            throw new DomainException("park_settings.invalid_horizon", "Počet dní dopředu musí být 1 až 366.");
        }

        SlotDurationMinutes = slotDurationMinutes;
        Capacity = capacity;
        PublicHoursDaysAhead = publicHoursDaysAhead;
        PublicBookingDaysAhead = publicBookingDaysAhead;
    }
}
