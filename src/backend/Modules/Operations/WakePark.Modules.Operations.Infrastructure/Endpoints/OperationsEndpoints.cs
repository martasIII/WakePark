using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Identity.Contracts;
using WakePark.Modules.Operations.Application.Schedule;
using WakePark.Modules.Operations.Contracts;

namespace WakePark.Modules.Operations.Infrastructure.Endpoints;

/// <summary>HTTP rozhraní modulu Operations.</summary>
public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/operations")
            .WithTags("Operations");

        group.MapGet("/cables/{cableId:guid}/schedule", GetScheduleAsync)
            .WithName("GetCableSchedule")
            .WithSummary("Rozvrh vleku pro daný den")
            .WithDescription("""
                Vypočítá, kdy se na vleku daný den jezdí. Vychází z provozní doby platné sezóny
                a promítá do ní výjimky (uzavírka, zkrácený provoz).

                Parametr `date` je v **lokálním čase areálu**, začátky slotů se vracejí v **UTC**.
                Přechod na letní čas je ošetřený: neexistující hodina se vynechá, dvojznačná
                se započítá jednou.

                Prázdný seznam znamená, že se ten den nejezdí — mimo sezónu, zavírací den
                nebo celodenní výjimka. Není to chyba.
                """)
            .Produces<IReadOnlyList<SlotTemplateDto>>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(PermissionPolicies.For(Permissions.OperationsRead));

        return endpoints;
    }

    private static async Task<IResult> GetScheduleAsync(
        Guid cableId,
        DateOnly date,
        IQueryHandler<GetScheduleQuery, IReadOnlyList<SlotTemplateDto>> handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SlotTemplateDto> schedule = await handler
            .HandleAsync(new GetScheduleQuery(cableId, date), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(schedule);
    }
}
