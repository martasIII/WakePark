using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Reservations.Application.Availability;
using WakePark.Modules.Reservations.Application.Reservations.CancelReservation;
using WakePark.Modules.Reservations.Application.Reservations.CreateReservation;
using WakePark.Modules.Reservations.Contracts;
using WakePark.Modules.Reservations.Domain.Reservations;

namespace WakePark.Modules.Reservations.Infrastructure.Endpoints;

/// <summary>HTTP rozhraní modulu Reservations.</summary>
public static class ReservationsEndpoints
{
    public static IEndpointRouteBuilder MapReservationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/reservations")
            .WithTags("Reservations");

        group.MapGet("/availability", GetAvailabilityAsync)
            .WithName("GetAvailability")
            .WithSummary("Volná kapacita vleku pro daný den")
            .Produces<IReadOnlyList<SlotAvailabilityDto>>();

        group.MapPost("/", CreateAsync)
            .WithName("CreateReservation")
            .WithSummary("Vytvoří rezervaci")
            .Produces<ReservationDto>(StatusCodes.Status201Created);

        group.MapPost("/{reservationId:guid}/cancel", CancelAsync)
            .WithName("CancelReservation")
            .WithSummary("Stornuje rezervaci podle politiky areálu")
            .Produces<ReservationDto>();

        return endpoints;
    }

    private static async Task<IResult> GetAvailabilityAsync(
        Guid cableId,
        DateOnly date,
        IQueryHandler<GetAvailabilityQuery, IReadOnlyList<SlotAvailabilityDto>> handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SlotAvailabilityDto> availability = await handler
            .HandleAsync(new GetAvailabilityQuery(cableId, date), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(availability);
    }

    private static async Task<IResult> CreateAsync(
        CreateReservationRequest request,
        ICommandHandler<CreateReservationCommand, ReservationDto> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ReservationDto reservation = await handler
            .HandleAsync(
                new CreateReservationCommand(
                    request.CableId,
                    request.StartUtc,
                    request.CustomerId,
                    request.Seats,
                    request.Source,
                    request.RidePassId,
                    request.IdempotencyKey),
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Created($"/api/v1/reservations/{reservation.Id}", reservation);
    }

    private static async Task<IResult> CancelAsync(
        Guid reservationId,
        CancelReservationRequest request,
        ICommandHandler<CancelReservationCommand, ReservationDto> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ReservationDto reservation = await handler
            .HandleAsync(new CancelReservationCommand(reservationId, request.Reason), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(reservation);
    }
}

/// <summary>Tělo požadavku na vytvoření rezervace.</summary>
public sealed record CreateReservationRequest(
    Guid CableId,
    DateTimeOffset StartUtc,
    Guid CustomerId,
    int Seats,
    ReservationSource Source,
    Guid? RidePassId,
    string? IdempotencyKey);

/// <summary>Tělo požadavku na storno.</summary>
public sealed record CancelReservationRequest(string Reason);
