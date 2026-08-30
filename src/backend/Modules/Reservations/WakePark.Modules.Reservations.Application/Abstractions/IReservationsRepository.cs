using WakePark.Modules.Reservations.Domain.Passes;
using WakePark.Modules.Reservations.Domain.Reservations;
using WakePark.Modules.Reservations.Domain.Slots;

namespace WakePark.Modules.Reservations.Application.Abstractions;

/// <summary>Přístup k rezervacím, slotům a permanentkám. Implementace je v infrastruktuře.</summary>
public interface IReservationsRepository
{
    /// <summary>Slot v daném čase, pokud už byl materializovaný. Načítá se pro zápis.</summary>
    Task<Slot?> FindSlotAsync(Guid cableId, DateTimeOffset startUtc, CancellationToken cancellationToken = default);

    Task<Slot?> GetSlotByIdAsync(Guid slotId, CancellationToken cancellationToken = default);

    /// <summary>Materializované sloty vleku v daném rozsahu. Jen pro čtení.</summary>
    Task<IReadOnlyList<Slot>> GetSlotsAsync(
        Guid cableId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    void AddSlot(Slot slot);

    Task<Reservation?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<Reservation?> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);

    void AddReservation(Reservation reservation);

    /// <summary>Permanentka včetně pohybů kreditu — bez nich nelze spočítat zůstatek.</summary>
    Task<RidePass?> GetRidePassAsync(Guid ridePassId, CancellationToken cancellationToken = default);
}
