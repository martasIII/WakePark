using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Application.Admin;
using WakePark.Modules.Catalog.Application.Catalog;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Identity.Contracts;

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
                Kategorie a aktivní produkty e-shopu s dnes platnou cenou včetně DPH, seřazené pro
                zobrazení. Texty jsou slovníky jazyk → text, čeština je vždy. Veřejné — čte ho web
                bez přihlášení. Neznámý provozovatel vrátí prázdnou nabídku.
                """)
            .Produces<CatalogDto>()
            .AllowAnonymous();

        group.MapGet("/enterprises/{enterpriseId:guid}/price-list", GetPriceListAsync)
            .WithName("GetPublicPriceList")
            .WithSummary("Ceník na webu")
            .WithDescription("""
                Sekce (kategorie) s podsekcemi a položkami s dnes platnou cenou. Sekce bez položek
                je v ceníku jen tehdy, když má popis. Veřejné.
                """)
            .Produces<PublicPriceListDto>()
            .AllowAnonymous();

        MapAdmin(group.MapGroup("/admin")
            .WithTags("Catalog — backoffice")
            .RequireAuthorization(PermissionPolicies.For(Permissions.CatalogManage)));

        return endpoints;
    }

    /// <summary>
    /// Správa katalogu (ADR 0009). Provozovatel je z přihlášení, ne z adresy (ADR 0007).
    /// Každá změna vrací celý katalog po změně.
    /// </summary>
    private static void MapAdmin(RouteGroupBuilder admin)
    {
        admin.MapGet("/", (IQueryHandler<GetCatalogAdminQuery, CatalogAdminDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new GetCatalogAdminQuery(), ct)))
            .WithName("GetCatalogAdmin")
            .WithSummary("Celý katalog pro backoffice")
            .WithDescription("I skryté produkty, platná cena a ceny, které teprve začnou platit.")
            .Produces<CatalogAdminDto>();

        admin.MapPost("/categories", (CreateCategoryRequest request, ICommandHandler<CreateCategoryCommand, CatalogAdminDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new CreateCategoryCommand(request), ct)))
            .WithName("CreateCategory")
            .WithSummary("Nová kategorie")
            .WithDescription("Kód je stálý. `parentId` zařadí kategorii jako podkategorii (nejvýš dvě úrovně).")
            .Produces<CatalogAdminDto>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapPut("/categories/{categoryId:guid}", (Guid categoryId, UpdateCategoryRequest request, ICommandHandler<UpdateCategoryCommand, CatalogAdminDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new UpdateCategoryCommand(categoryId, request), ct)))
            .WithName("UpdateCategory")
            .WithSummary("Úprava kategorie")
            .Produces<CatalogAdminDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapDelete("/categories/{categoryId:guid}", (Guid categoryId, ICommandHandler<DeleteCategoryCommand, CatalogAdminDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new DeleteCategoryCommand(categoryId), ct)))
            .WithName("DeleteCategory")
            .WithSummary("Smazání prázdné kategorie")
            .WithDescription("Kategorii s produkty nebo podkategoriemi smazat nejde (422).")
            .Produces<CatalogAdminDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapPost("/products", (CreateProductRequest request, ICommandHandler<CreateProductCommand, CatalogAdminDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new CreateProductCommand(request), ct)))
            .WithName("CreateProduct")
            .WithSummary("Nový produkt")
            .WithDescription("Kód je stálý. Cena se nastaví zvlášť; produkt bez ceny se nikde nenabízí.")
            .Produces<CatalogAdminDto>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapPut("/products/{productId:guid}", (Guid productId, UpdateProductRequest request, ICommandHandler<UpdateProductCommand, CatalogAdminDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new UpdateProductCommand(productId, request), ct)))
            .WithName("UpdateProduct")
            .WithSummary("Úprava produktu")
            .WithDescription("Texty, kategorie, pořadí, kde se ukazuje, a skrytí. Produkt se nemaže, jen skryje.")
            .Produces<CatalogAdminDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapPut("/products/{productId:guid}/price", (Guid productId, SetPriceRequest request, ICommandHandler<SetProductPriceCommand, CatalogAdminDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new SetProductPriceCommand(productId, request), ct)))
            .WithName("SetProductPrice")
            .WithSummary("Nová cena produktu")
            .WithDescription("""
                Bez `validFrom` platí od dneška, jinak od zvoleného dne (čas areálu). Zpětně ne.
                Cena v e-shopu musí být kladná. Prodané objednávky a vouchery se nemění.
                """)
            .Produces<CatalogAdminDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapDelete("/products/{productId:guid}/price/{validFrom}", (Guid productId, DateOnly validFrom, ICommandHandler<RemoveScheduledPriceCommand, CatalogAdminDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new RemoveScheduledPriceCommand(productId, validFrom), ct)))
            .WithName("RemoveScheduledPrice")
            .WithSummary("Zrušení ceny, která teprve začne platit")
            .Produces<CatalogAdminDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapPut("/price-list/title", (SetPriceListTitleRequest request, ICommandHandler<SetPriceListTitleCommand, CatalogAdminDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new SetPriceListTitleCommand(request), ct)))
            .WithName("SetPriceListTitle")
            .WithSummary("Podtitulek ceníku na webu")
            .Produces<CatalogAdminDto>();
    }

    private static async Task<IResult> Ok<T>(Task<T> result) => Results.Ok(await result.ConfigureAwait(false));

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

    private static async Task<IResult> GetPriceListAsync(
        Guid enterpriseId,
        IQueryHandler<GetPublicPriceListQuery, PublicPriceListDto> handler,
        CancellationToken cancellationToken)
    {
        PublicPriceListDto priceList = await handler
            .HandleAsync(new GetPublicPriceListQuery(enterpriseId), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(priceList);
    }
}
