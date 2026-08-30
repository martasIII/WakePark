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
            .WithDescription("""
                Spojí rozvrh (co by se dalo jezdit) s obsazeností (co je zabrané).

                Kapacita **není** počet lan — na laně je vždy jeden jezdec. Je to počet lidí,
                kteří se v bloku vystřídají.

                Parametr `date` je v lokálním čase areálu.
                """)
            .Produces<IReadOnlyList<SlotAvailabilityDto>>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/", CreateAsync)
            .WithName("CreateReservation")
            .WithSummary("Vytvoří rezervaci")
            .WithDescription("""
                Zabere místa v bloku a případně odečte jízdy z permanentky — obojí v jedné
                transakci, takže rezervace bez odečtu (ani naopak) vzniknout nemůže.

                Slot v databázi vzniká **až s první rezervací**; kalendář se dopředu negeneruje.

                `seats` je počet jezdců. Každé místo je jedna jízda, rodina tří osob tedy
                odečte 3 jízdy.

                `idempotencyKey` chrání proti dvojímu odeslání: opakovaný požadavek se stejným
                klíčem vrátí původní rezervaci a nic nového nevytvoří.

                `ridePassId` je volitelné — bez něj se rezervace jen potvrdí a platba se řeší jinak.
                """)
            .Produces<ReservationDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{reservationId:guid}/cancel", CancelAsync)
            .WithName("CancelReservation")
            .WithSummary("Stornuje rezervaci podle politiky areálu")
            .WithDescription("""
                Uvolní místa ve slotu a vrátí poměrnou část kreditu. Kolik se vrátí, určuje
                politika areálu (`freeCancellationHours`, `lateCancellationRefundPercent`),
                ne konstanta v kódu.

                Vrácené jízdy se zaokrouhlují dolů: 3 místa při 50 % znamenají 1 jízdu zpět.

                Kredit se vrací **novým pohybem**, původní se needituje — kniha kreditu je
                append-only.
                """)
            .Produces<ReservationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

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
