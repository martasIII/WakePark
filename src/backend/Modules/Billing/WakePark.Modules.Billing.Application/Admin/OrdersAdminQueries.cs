using System.Globalization;

using WakePark.BuildingBlocks.Application;
using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Domain.Invoices;
using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Billing.Domain.Payments;
using WakePark.Modules.Vouchers.Contracts;

namespace WakePark.Modules.Billing.Application.Admin;

// Přehled objednávek v backoffice — jen čtení (ADR 0012). Provozovatel je z přihlášení
// a každý dotaz se na něj omezuje sám (ADR 0007).

/// <summary>Seznam objednávek. <c>Status</c> volitelně <c>AwaitingPayment</c> / <c>Paid</c> / <c>Cancelled</c>.</summary>
public sealed record GetOrdersAdminQuery(string? Status, string? Search, int Page = 1, int PageSize = 25) : IQuery<OrderListDto>;

public sealed record GetOrderAdminQuery(long Number) : IQuery<OrderAdminDetailDto>;

public sealed record GetUnmatchedPaymentsQuery : IQuery<IReadOnlyList<UnmatchedPaymentDto>>;

internal sealed class GetOrdersAdminQueryHandler(IOrderRepository orders, ITenantContext tenant)
    : IQueryHandler<GetOrdersAdminQuery, OrderListDto>
{
    public const int MaxPageSize = 100;

    public async Task<OrderListDto> HandleAsync(GetOrdersAdminQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        Guid enterpriseId = OrdersAdmin.EnterpriseOf(tenant);

        OrderStatus? status = query.Status is { Length: > 0 } text
            ? Enum.TryParse(text, ignoreCase: true, out OrderStatus parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new DomainException($"Neznámý stav objednávky „{text}“.")
            : null;
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        (IReadOnlyList<Order> found, int total) = await orders
            .SearchAsync(enterpriseId, status, query.Search, (page - 1) * pageSize, pageSize, cancellationToken)
            .ConfigureAwait(false);

        return new OrderListDto(
            [.. found.Select(o => new OrderListItemDto(
                o.Number,
                o.Status.ToString(),
                o.PlacedAtUtc,
                o.PaidAtUtc,
                o.Customer.Name,
                o.Customer.Email,
                o.Customer.CompanyName,
                OrdersAdmin.Summary(o),
                o.TotalGross,
                o.Currency))],
            total,
            page,
            pageSize);
    }
}

internal sealed class GetOrderAdminQueryHandler(
    IOrderRepository orders,
    IInvoiceRepository invoices,
    IBankTransactionRepository bankTransactions,
    IVoucherDirectory vouchers,
    ITenantContext tenant)
    : IQueryHandler<GetOrderAdminQuery, OrderAdminDetailDto>
{
    public async Task<OrderAdminDetailDto> HandleAsync(GetOrderAdminQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        Guid enterpriseId = OrdersAdmin.EnterpriseOf(tenant);

        Order order = await orders.GetByNumberAsync(enterpriseId, query.Number, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Objednávka {query.Number} neexistuje.");

        Invoice? invoice = await invoices.GetByOrderAsync(order.Id, cancellationToken).ConfigureAwait(false);
        BankTransaction? payment = order.PaidByBankTransactionId is { } paymentId
            ? await bankTransactions.GetAsync(enterpriseId, paymentId, cancellationToken).ConfigureAwait(false)
            : null;
        IReadOnlyList<VoucherDto> issued = await vouchers.GetByOrderAsync(order.Id, cancellationToken).ConfigureAwait(false);

        return new OrderAdminDetailDto(
            order.Number,
            order.Status.ToString(),
            order.Language,
            order.PlacedAtUtc,
            order.PaymentDueAtUtc,
            order.PaymentReminderAtUtc,
            order.PaidAtUtc,
            order.CancelledAtUtc,
            Customer(order.Customer),
            [.. order.Lines.Select(l => new OrderLineDto(
                l.ProductCode, l.DisplayName, l.Quantity, l.UnitPrice, l.VatRate, l.LineGross, l.ProductId, l.Dedications))],
            order.TotalGross,
            order.TotalVat,
            order.TotalNet,
            order.Currency,
            payment is null
                ? null
                : new OrderPaymentDto(payment.BookedOn, payment.Amount, payment.Currency, payment.VariableSymbol,
                    payment.CounterpartyAccount, payment.CounterpartyName),
            invoice?.Number,
            [.. issued
                .OrderBy(v => v.ProductCode, StringComparer.Ordinal)
                .ThenBy(v => v.Piece)
                .Select(v => new OrderVoucherDto(v.Code, v.ProductCode, v.Name, v.Piece, v.Status, v.Dedication))],
            order.AccessToken);
    }

    private static OrderCustomerDto Customer(Domain.Customers.CustomerContact c) =>
        new(c.Name, c.Email, c.Phone, c.CompanyName, c.CompanyId, c.VatId, c.CompanyAddress);
}

internal sealed class GetUnmatchedPaymentsQueryHandler(IBankTransactionRepository bankTransactions, ITenantContext tenant)
    : IQueryHandler<GetUnmatchedPaymentsQuery, IReadOnlyList<UnmatchedPaymentDto>>
{
    /// <summary>Víc jich v provozu být nemá; kdyby ano, je to signál k úklidu, ne ke stránkování.</summary>
    public const int Limit = 200;

    public async Task<IReadOnlyList<UnmatchedPaymentDto>> HandleAsync(GetUnmatchedPaymentsQuery query, CancellationToken cancellationToken = default)
    {
        Guid enterpriseId = OrdersAdmin.EnterpriseOf(tenant);

        IReadOnlyList<BankTransaction> unmatched = await bankTransactions
            .GetUnmatchedAsync(enterpriseId, Limit, cancellationToken)
            .ConfigureAwait(false);

        return [.. unmatched.Select(t => new UnmatchedPaymentDto(
            t.Id, t.BookedOn, t.Amount, t.Currency, t.VariableSymbol, t.CounterpartyAccount, t.CounterpartyName, t.Message, t.Note))];
    }
}

internal static class OrdersAdmin
{
    /// <summary>Bez provozovatele v přihlášení nejsou žádná data — stejně jako u katalogu.</summary>
    public static Guid EnterpriseOf(ITenantContext tenant) =>
        tenant.EnterpriseId ?? throw new NotFoundException("Objednávky nenalezeny.");

    /// <summary>„2× Voucher pro dva, 1× 10 × 20 minut“.</summary>
    public static string Summary(Order order) =>
        string.Join(", ", order.Lines.Select(l => string.Create(CultureInfo.InvariantCulture, $"{l.Quantity}× {l.DisplayName}")));
}
