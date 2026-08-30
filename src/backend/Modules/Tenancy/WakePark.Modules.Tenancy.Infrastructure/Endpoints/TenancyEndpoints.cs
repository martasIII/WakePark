using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Identity.Contracts;
using WakePark.Modules.Tenancy.Application.Parks.GetParks;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Infrastructure.Endpoints;

/// <summary>HTTP rozhraní modulu Tenancy.</summary>
public static class TenancyEndpoints
{
    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/tenancy")
            .WithTags("Tenancy");

        group.MapGet("/parks", GetAllParksAsync)
            .WithName("GetAllParks")
            .WithSummary("Seznam všech areálů")
            .WithDescription("""
                Areály napříč provozovateli, seřazené podle názvu.

                Až přibude autentizace, bude se seznam omezovat na tenanta přihlášeného
                uživatele. Dokud modul Identity neexistuje, je otevřený.
                """)
            .Produces<IReadOnlyList<ParkDto>>()
            .RequireAuthorization(PermissionPolicies.For(Permissions.ParksRead));

        group.MapGet("/enterprises/{enterpriseId:guid}/parks", GetParksAsync)
            .WithName("GetParks")
            .WithSummary("Seznam areálů provozovatele")
            .WithDescription(
                "Vrací i politiku storna areálu — podle ní se řídí, kolik se vrátí při zrušení rezervace. "
                + "Neznámý provozovatel není chyba, vrátí se prázdný seznam.")
            .Produces<IReadOnlyList<ParkDto>>()
            .RequireAuthorization(PermissionPolicies.For(Permissions.ParksRead));

        return endpoints;
    }

    private static async Task<IResult> GetAllParksAsync(
        IQueryHandler<GetAllParksQuery, IReadOnlyList<ParkDto>> handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ParkDto> parks = await handler
            .HandleAsync(new GetAllParksQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(parks);
    }

    private static async Task<IResult> GetParksAsync(
        Guid enterpriseId,
        IQueryHandler<GetParksQuery, IReadOnlyList<ParkDto>> handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ParkDto> parks = await handler
            .HandleAsync(new GetParksQuery(enterpriseId), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(parks);
    }
}
