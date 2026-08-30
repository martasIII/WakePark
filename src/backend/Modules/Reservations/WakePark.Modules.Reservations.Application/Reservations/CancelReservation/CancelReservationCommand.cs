using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Reservations.Contracts;

namespace WakePark.Modules.Reservations.Application.Reservations.CancelReservation;

/// <summary>Storno rezervace. Kolik se vrátí, rozhoduje politika areálu.</summary>
public sealed record CancelReservationCommand(Guid ReservationId, string Reason)
    : ICommand<ReservationDto>;
