using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Reservations.Application.Abstractions;
using WakePark.Modules.Reservations.Domain.Passes;
using WakePark.Modules.Reservations.Domain.Reservations;
using WakePark.Modules.Reservations.Domain.Slots;

namespace WakePark.Modules.Reservations.Infrastructure.Persistence.Repositories;

internal sealed class ReservationsRepository(ReservationsDbContext dbContext) : IReservationsRepository
{
    public async Task<Slot?> FindSlotAsync(
        Guid cableId,
        DateTimeOffset startUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.Slots
            .FirstOrDefaultAsync(
                slot => slot.CableId == cableId && slot.StartUtc == startUtc,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<Slot?> GetSlotByIdAsync(Guid slotId, CancellationToken cancellationToken = default) =>
        await dbContext.Slots
            .FirstOrDefaultAsync(slot => slot.Id == slotId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Slot>> GetSlotsAsync(
        Guid cableId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.Slots
            .AsNoTracking()
            .Where(slot => slot.CableId == cableId && slot.StartUtc >= fromUtc && slot.StartUtc < toUtc)
            .OrderBy(slot => slot.StartUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void AddSlot(Slot slot) => dbContext.Slots.Add(slot);

    public async Task<Reservation?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        await dbContext.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                reservation => reservation.IdempotencyKey == idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<Reservation?> GetReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Reservations
            .FirstOrDefaultAsync(reservation => reservation.Id == reservationId, cancellationToken)
            .ConfigureAwait(false);

    public void AddReservation(Reservation reservation) => dbContext.Reservations.Add(reservation);

    public async Task<RidePass?> GetRidePassAsync(
        Guid ridePassId,
        CancellationToken cancellationToken = default) =>
        await dbContext.RidePasses
            .Include(pass => pass.Transactions)
            .FirstOrDefaultAsync(pass => pass.Id == ridePassId, cancellationToken)
            .ConfigureAwait(false);
}
