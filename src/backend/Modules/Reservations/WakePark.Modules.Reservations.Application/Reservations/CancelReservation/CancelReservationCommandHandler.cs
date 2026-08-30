using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Reservations.Application.Abstractions;
using WakePark.Modules.Reservations.Contracts;
using WakePark.Modules.Reservations.Domain.Passes;
using WakePark.Modules.Reservations.Domain.Reservations;
using WakePark.Modules.Reservations.Domain.Slots;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Reservations.Application.Reservations.CancelReservation;

/// <summary>
/// Stornuje rezervaci: uvolní místo ve slotu a vrátí odpovídající část kreditu.
/// Pravidla nejsou zadrátovaná v kódu — čtou se z politiky areálu.
/// </summary>
internal sealed class CancelReservationCommandHandler(
    IReservationsRepository repository,
    ICableLocator cableLocator,
    IParkDirectory parkDirectory,
    IReservationsUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CancelReservationCommand, ReservationDto>
{
    public async Task<ReservationDto> HandleAsync(
        CancelReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Reservation reservation = await repository
            .GetReservationAsync(command.ReservationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("reservation.not_found", "Rezervace neexistuje.");

        Slot slot = await repository
            .GetSlotByIdAsync(reservation.SlotId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("slot.not_found", "Slot rezervace neexistuje.");

        CancellationPolicyDto policy = await GetPolicyAsync(slot.CableId, cancellationToken)
            .ConfigureAwait(false);

        if (!policy.CancellationAllowed)
        {
            throw new DomainException(
                "reservation.cancellation_not_allowed",
                "Areál storno rezervací neumožňuje.");
        }

        DateTimeOffset now = clock.UtcNow;
        TimeSpan notice = slot.StartUtc - now;

        decimal refundPercent = notice >= TimeSpan.FromHours(policy.FreeCancellationHours)
            ? 100m
            : policy.LateCancellationRefundPercent;

        reservation.Cancel(now, command.Reason, refundPercent);
        slot.Release(reservation.Seats);

        // Kredit se vrací jen v poměru, který politika areálu připouští.
        if (reservation.RidePassId is { } ridePassId)
        {
            RidePass pass = await repository
                .GetRidePassAsync(ridePassId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new DomainException("ride_pass.not_found", "Permanentka neexistuje.");

            pass.Refund(reservation.RefundableRides(), reservation.Id, now, command.Reason);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ReservationDto(
            reservation.Id,
            reservation.SlotId,
            reservation.CustomerId,
            reservation.RidePassId,
            reservation.OrderId,
            slot.StartUtc,
            reservation.Seats,
            reservation.Status.ToString(),
            reservation.Source.ToString(),
            reservation.CancelledAtUtc,
            reservation.RefundPercent);
    }

    private async Task<CancellationPolicyDto> GetPolicyAsync(Guid cableId, CancellationToken cancellationToken)
    {
        Guid wakeParkId = await cableLocator
            .FindWakeParkIdAsync(cableId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("cable.not_found", "Vlek neexistuje.");

        ParkDto park = await parkDirectory
            .GetParkAsync(wakeParkId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("park.not_found", "Areál neexistuje.");

        return park.CancellationPolicy;
    }
}
