using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Identity.Contracts;
using WakePark.Modules.Operations.Application.Calendar;
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
                Vypočítá, kdy se na vleku daný den jezdí: provozní den areálu rozpadnutý na bloky
                podle nastavení areálu, bez bloků, které zasahuje blokace vleku nebo areálu.

                Parametr `date` je v **lokálním čase areálu**, začátky slotů se vracejí v **UTC**.
                Přechod na letní čas je ošetřený: neexistující hodina se vynechá, dvojznačná
                se započítá jednou.

                Prázdný seznam znamená, že se ten den nejezdí — zavřeno nebo celodenní blokace.
                Není to chyba.
                """)
            .Produces<IReadOnlyList<SlotTemplateDto>>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(PermissionPolicies.For(Permissions.OperationsRead));

        RouteGroupBuilder park = group.MapGroup("/parks/{wakeParkId:guid}");

        park.MapGet("/opening-hours", (Guid wakeParkId, IQueryHandler<GetPublicOpeningHoursQuery, PublicOpeningHoursDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new GetPublicOpeningHoursQuery(wakeParkId), ct)))
            .WithName("GetPublicOpeningHours")
            .WithSummary("Otevírací doba na webu")
            .WithDescription("""
                Dny od dneška tolik dopředu, kolik má areál nastaveno (14). Den bez časů = zavřeno.
                Blokace ořezané na otevírací dobu, jen s důvodem — interní poznámka se nevrací.
                Veřejné.
                """)
            .Produces<PublicOpeningHoursDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        park.MapGet("/calendar", (Guid wakeParkId, DateOnly from, DateOnly to, IQueryHandler<GetOperatingCalendarQuery, OperatingCalendarDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new GetOperatingCalendarQuery(wakeParkId, from, to), ct)))
            .WithName("GetOperatingCalendar")
            .WithSummary("Provozní kalendář (backoffice)")
            .WithDescription("Otevřené dny a blokace v rozsahu `from`–`to` (obě meze včetně, nejvýš 400 dní).")
            .Produces<OperatingCalendarDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PermissionPolicies.For(Permissions.OperationsRead));

        park.MapPut("/days/{date}", (Guid wakeParkId, DateOnly date, SetOperatingDayRequest request, ICommandHandler<SetOperatingDayCommand, OperatingCalendarDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new SetOperatingDayCommand(wakeParkId, date, request), ct)))
            .WithName("SetOperatingDay")
            .WithSummary("Otevírací doba jednoho dne")
            .WithDescription("Oba časy null = ten den zavřeno.")
            .Produces<OperatingCalendarDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(PermissionPolicies.For(Permissions.OperationsManage));

        park.MapPost("/days/import/preview", (Guid wakeParkId, OperatingDaysImportRequest request, IQueryHandler<PreviewOperatingDaysImportQuery, OperatingDaysImportDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new PreviewOperatingDaysImportQuery(wakeParkId, request.Csv), ct)))
            .WithName("PreviewOperatingDaysImport")
            .WithSummary("Náhled importu provozní doby z CSV")
            .WithDescription("""
                Řádky `datum;od;do` (oddělovač ; , nebo tab, datum 2027-05-01 i 1.5.2027, hlavička
                smí být). Bez časů = zavřeno. Nic neuloží — vrátí, co se změní, a chyby po řádcích.
                """)
            .Produces<OperatingDaysImportDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PermissionPolicies.For(Permissions.OperationsManage));

        park.MapPost("/days/import", (Guid wakeParkId, OperatingDaysImportRequest request, ICommandHandler<ApplyOperatingDaysImportCommand, OperatingDaysImportDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new ApplyOperatingDaysImportCommand(wakeParkId, request.Csv), ct)))
            .WithName("ApplyOperatingDaysImport")
            .WithSummary("Import provozní doby z CSV")
            .WithDescription("S chybou v souboru neuloží nic (422). Dny, které v CSV nejsou, zůstanou.")
            .Produces<OperatingDaysImportDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(PermissionPolicies.For(Permissions.OperationsManage));

        park.MapPost("/blockings", (Guid wakeParkId, CreateBlockingRequest request, ICommandHandler<CreateBlockingCommand, BlockingDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new CreateBlockingCommand(wakeParkId, request), ct)))
            .WithName("CreateBlocking")
            .WithSummary("Blokace vleku nebo areálu")
            .WithDescription("""
                Začátek a konec v lokálním čase areálu; bez `cableId` = celý areál. Kategorie:
                TechnicalFault, PrivateEvent, Weather, Other. Smí i obsluha.
                """)
            .Produces<BlockingDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(PermissionPolicies.For(Permissions.BlockingsManage));

        park.MapDelete("/blockings/{blockingId:guid}", async (Guid wakeParkId, Guid blockingId, ICommandHandler<DeleteBlockingCommand, bool> handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(new DeleteBlockingCommand(wakeParkId, blockingId), ct).ConfigureAwait(false);
                return Results.NoContent();
            })
            .WithName("DeleteBlocking")
            .WithSummary("Zrušení blokace")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PermissionPolicies.For(Permissions.BlockingsManage));

        park.MapGet("/cables", (Guid wakeParkId, IQueryHandler<GetCablesQuery, IReadOnlyList<CableDetailDto>> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new GetCablesQuery(wakeParkId), ct)))
            .WithName("GetParkCables")
            .WithSummary("Vleky areálu s parametry a překážkami")
            .Produces<IReadOnlyList<CableDetailDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(PermissionPolicies.For(Permissions.OperationsRead));

        return endpoints;
    }

    private static async Task<IResult> Ok<T>(Task<T> result) => Results.Ok(await result.ConfigureAwait(false));

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
