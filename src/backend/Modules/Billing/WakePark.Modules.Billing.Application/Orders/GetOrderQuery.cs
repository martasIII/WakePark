using System.Globalization;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Application.Payments;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Domain.Orders;

namespace WakePark.Modules.Billing.Application.Orders;

/// <summary>
/// Objednávka podle přístupového klíče z adresy. Neexistující i nesprávný klíč vrátí
/// <c>null</c> — z odpovědi nejde poznat, jestli objednávka existuje.
/// </summary>
public sealed record GetOrderQuery(string AccessToken) : IQuery<OrderDto?>;

internal sealed class GetOrderQueryHandler(IOrderRepository orders, BillingOptions options)
    : IQueryHandler<GetOrderQuery, OrderDto?>
{
    public async Task<OrderDto?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.AccessToken is not { Length: Order.AccessTokenLength })
        {
            return null;
        }

        Order? order = await orders
            .GetByAccessTokenAsync(query.AccessToken, cancellationToken)
            .ConfigureAwait(false);

        return order is null ? null : ToDto(order);
    }

    private OrderDto ToDto(Order order) => new(
        order.Number,
        order.Status.ToString(),
        order.Language,
        order.Customer.Name,
        order.Customer.Email,
        order.Customer.CompanyName,
        [.. order.Lines.Select(l => new OrderLineDto(
            l.ProductCode, l.DisplayName, l.Quantity, l.UnitPrice, l.VatRate, l.LineGross, l.ProductId))],
        order.TotalGross,
        order.TotalVat,
        order.TotalNet,
        order.Currency,
        order.PlacedAtUtc,
        order.PaymentDueAtUtc,
        order.Status == OrderStatus.AwaitingPayment ? PaymentInstructions(order) : null);

    private PaymentInstructionsDto PaymentInstructions(Order order)
    {
        PaymentAccountOptions account = options.PaymentAccount;
        string variableSymbol = order.Number.ToString(CultureInfo.InvariantCulture);
        string message = Spayd.Message($"{account.MessagePrefix} {variableSymbol}");

        return new PaymentInstructionsDto(
            account.AccountNumber,
            account.Iban,
            account.Bic,
            variableSymbol,
            order.TotalGross,
            order.Currency,
            message,
            Spayd.Build(account.Iban, account.Bic, order.TotalGross, order.Currency, variableSymbol, message));
    }
}
