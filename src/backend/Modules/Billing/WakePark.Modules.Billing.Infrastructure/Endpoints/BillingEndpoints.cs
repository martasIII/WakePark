using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Application.Admin;
using WakePark.Modules.Billing.Application.Customers;
using WakePark.Modules.Billing.Application.Orders;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Identity.Contracts;

namespace WakePark.Modules.Billing.Infrastructure.Endpoints;

/// <summary>HTTP rozhraní modulu Billing.</summary>
/// <remarks>
/// Objednávkové endpointy jsou anonymní — volá je web za zákazníka. Dnes je API dostupné jen
/// z vnitřní sítě Dockeru; před otevřením přes gateway musí dostat vlastní ochranu (ADR 0006).
/// </remarks>
public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/billing")
            .WithTags("Billing");

        group.MapPost("/enterprises/{enterpriseId:guid}/orders", PlaceOrderAsync)
            .WithName("PlaceOrder")
            .WithSummary("Založit objednávku z e-shopu")
            .WithDescription("""
                Ceny a názvy se berou z aktuální nabídky, ne z požadavku. Vrací číslo objednávky
                (= variabilní symbol) a přístupový klíč ke stránce objednávky. Neplatný požadavek
                vrátí 400 (formulář) nebo 422 (doménové pravidlo, např. neplatné IČO).
                """)
            .Produces<PlacedOrderDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .AllowAnonymous()
            // CSRF hrozí jen tam, kde prohlížeč sám přiloží přihlašovací cookie. Objednávka
            // přihlášení nepoužívá a volá ji server webu, ne prohlížeč — token nemá co chránit.
            .DisableAntiforgery();

        group.MapGet("/orders/{accessToken}", GetOrderAsync)
            .WithName("GetOrder")
            .WithSummary("Objednávka podle přístupového klíče")
            .WithDescription("U objednávky, která čeká na platbu, i platební údaje a obsah QR kódu.")
            .Produces<OrderDto>()
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        group.MapGet("/companies/{companyId}", FindCompanyAsync)
            .WithName("FindCompany")
            .WithSummary("Firma podle IČO z registru ARES")
            .WithDescription("""
                Pro náhled ve formuláři e-shopu: název, DIČ a sídlo. Neplatné IČO, neexistující
                i zaniklá firma vrátí 404, výpadek ARES 503.
                """)
            .Produces<CompanyDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable)
            .AllowAnonymous();

        MapAdmin(group.MapGroup("/admin")
            .WithTags("Billing — backoffice")
            .RequireAuthorization(PermissionPolicies.For(Permissions.OrdersRead)));

        return endpoints;
    }

    /// <summary>Přehled objednávek a plateb pro backoffice — jen čtení (ADR 0012). Provozovatel z přihlášení.</summary>
    private static void MapAdmin(RouteGroupBuilder admin)
    {
        admin.MapGet("/orders", (string? status, string? q, int? page, int? pageSize,
                IQueryHandler<GetOrdersAdminQuery, OrderListDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new GetOrdersAdminQuery(status, q, page ?? 1, pageSize ?? 25), ct)))
            .WithName("GetOrdersAdmin")
            .WithSummary("Seznam objednávek")
            .WithDescription("""
                Nejnovější první, po stránkách (nejvýš 100). `status`: `AwaitingPayment`, `Paid`,
                `Cancelled`. `q` hledá v čísle objednávky, jménu, e-mailu a firmě.
                """)
            .Produces<OrderListDto>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapGet("/orders/{number:long}", (long number, IQueryHandler<GetOrderAdminQuery, OrderAdminDetailDto> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new GetOrderAdminQuery(number), ct)))
            .WithName("GetOrderAdmin")
            .WithSummary("Detail objednávky")
            .WithDescription("Zákazník, položky s věnováním, platba, daňový doklad a vystavené poukazy.")
            .Produces<OrderAdminDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapGet("/payments/unmatched", (IQueryHandler<GetUnmatchedPaymentsQuery, IReadOnlyList<UnmatchedPaymentDto>> handler, CancellationToken ct) =>
                Ok(handler.HandleAsync(new GetUnmatchedPaymentsQuery(), ct)))
            .WithName("GetUnmatchedPayments")
            .WithSummary("Platby k ručnímu vyřízení")
            .WithDescription("Příchozí platby, které vypadají jako platba za objednávku, ale nesedí VS nebo částka.")
            .Produces<IReadOnlyList<UnmatchedPaymentDto>>();
    }

    private static async Task<IResult> Ok<T>(Task<T> result) => Results.Ok(await result.ConfigureAwait(false));

    private static async Task<IResult> FindCompanyAsync(
        string companyId,
        IQueryHandler<FindCompanyQuery, CompanyDto?> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            CompanyDto? company = await handler
                .HandleAsync(new FindCompanyQuery(companyId), cancellationToken)
                .ConfigureAwait(false);

            return company is null ? Results.NotFound() : Results.Ok(company);
        }
        catch (CompanyRegistryUnavailableException)
        {
            return Results.Problem("Registr firem ARES teď neodpovídá.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> PlaceOrderAsync(
        Guid enterpriseId,
        PlaceOrderRequest request,
        ICommandHandler<PlaceOrderCommand, PlacedOrderDto> handler,
        CancellationToken cancellationToken)
    {
        PlacedOrderDto placed = await handler
            .HandleAsync(new PlaceOrderCommand(enterpriseId, request), cancellationToken)
            .ConfigureAwait(false);

        return Results.Created($"/api/v1/billing/orders/{placed.AccessToken}", placed);
    }

    private static async Task<IResult> GetOrderAsync(
        string accessToken,
        IQueryHandler<GetOrderQuery, OrderDto?> handler,
        CancellationToken cancellationToken)
    {
        OrderDto? order = await handler
            .HandleAsync(new GetOrderQuery(accessToken), cancellationToken)
            .ConfigureAwait(false);

        return order is null ? Results.NotFound() : Results.Ok(order);
    }
}
