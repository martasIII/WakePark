using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
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

        group.MapGet("/enterprises/{enterpriseId:guid}/parks", GetParksAsync)
            .WithName("GetParks")
            .WithSummary("Seznam areálů provozovatele")
            .Produces<IReadOnlyList<ParkDto>>();

        return endpoints;
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
