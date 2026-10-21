using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Orders;
using WakePark.Modules.Billing.Contracts;

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
            .AllowAnonymous();

        group.MapGet("/orders/{accessToken}", GetOrderAsync)
            .WithName("GetOrder")
            .WithSummary("Objednávka podle přístupového klíče")
            .WithDescription("U objednávky, která čeká na platbu, i platební údaje a obsah QR kódu.")
            .Produces<OrderDto>()
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        return endpoints;
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
