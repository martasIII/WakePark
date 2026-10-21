using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Application.Catalog;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Catalog.Infrastructure.Endpoints;

/// <summary>HTTP rozhraní modulu Catalog.</summary>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/catalog")
            .WithTags("Catalog");

        group.MapGet("/enterprises/{enterpriseId:guid}", GetCatalogAsync)
            .WithName("GetCatalog")
            .WithSummary("Nabídka e-shopu")
            .WithDescription("""
                Kategorie a aktivní produkty s cenou včetně DPH, seřazené pro zobrazení. Texty jsou
                slovníky jazyk → text, čeština je vždy. Veřejné — čte ho web bez přihlášení.
                Neznámý provozovatel vrátí prázdnou nabídku.
                """)
            .Produces<CatalogDto>()
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> GetCatalogAsync(
        Guid enterpriseId,
        IQueryHandler<GetCatalogQuery, CatalogDto> handler,
        CancellationToken cancellationToken)
    {
        CatalogDto catalog = await handler
            .HandleAsync(new GetCatalogQuery(enterpriseId), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(catalog);
    }
}
