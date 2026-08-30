using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Reservations.Contracts;
using WakePark.Modules.Reservations.Domain.Reservations;

namespace WakePark.Modules.Reservations.Application.Reservations.CreateReservation;

/// <summary>
/// Rezervace míst v konkrétním bloku.
/// </summary>
/// <param name="CableId">Vlek.</param>
/// <param name="StartUtc">Začátek bloku podle rozvrhu.</param>
/// <param name="CustomerId">Zákazník.</param>
/// <param name="Seats">Kolik míst se rezervuje. Každé místo je jedna jízda.</param>
/// <param name="Source">Odkud rezervace přišla.</param>
/// <param name="RidePassId">Permanentka k čerpání. Null = platba jinak.</param>
/// <param name="IdempotencyKey">Ochrana proti dvojímu odeslání.</param>
public sealed record CreateReservationCommand(
    Guid CableId,
    DateTimeOffset StartUtc,
    Guid CustomerId,
    int Seats,
    ReservationSource Source,
    Guid? RidePassId,
    string? IdempotencyKey) : ICommand<ReservationDto>;
