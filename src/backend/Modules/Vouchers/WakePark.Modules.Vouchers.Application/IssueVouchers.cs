using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Vouchers.Application.Abstractions;
using WakePark.Modules.Vouchers.Contracts;
using WakePark.Modules.Vouchers.Domain;

namespace WakePark.Modules.Vouchers.Application;

/// <summary>Nastavení poukazů (sekce <c>Vouchers</c>).</summary>
public sealed class VouchersOptions
{
    public const string SectionName = "Vouchers";

    public string CodePrefix { get; init; } = "WD";

    /// <summary>Platnost vystavovaných poukazů — zatím pevné období, sezóna 2027 (ADR 0006).</summary>
    public DateOnly ValidFrom { get; init; } = new(2027, 5, 1);

    public DateOnly ValidTo { get; init; } = new(2027, 9, 30);
}

/// <summary>
/// Objednávka je zaplacená → vystaví se poukazy, jeden na každý kus, a zveřejní se
/// „poukazy vystavené“. Obojí v jedné transakci (outbox Vouchers).
/// </summary>
/// <remarks>
/// Idempotentní: když už k objednávce poukazy jsou, neudělá nic — outbox doručuje
/// „aspoň jednou“ a druhé doručení nesmí vystavit druhou sadu poukazů.
/// </remarks>
internal sealed class OrderPaidConsumer(
    IOrderDirectory orders,
    IVoucherRepository vouchers,
    IVouchersOutbox outbox,
    IVouchersUnitOfWork unitOfWork,
    IClock clock,
    VouchersOptions options)
    : IIntegrationEventConsumer<OrderPaidIntegrationEvent>
{
    public async Task HandleAsync(OrderPaidIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (await vouchers.AnyForOrderAsync(integrationEvent.OrderId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        OrderDto order = await orders.GetOrderAsync(integrationEvent.AccessToken, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Zaplacená objednávka {integrationEvent.OrderId} neexistuje.");

        DateTimeOffset now = clock.UtcNow;

        foreach (OrderLineDto line in order.Lines)
        {
            VoucherProduct product = new(line.ProductId, line.ProductCode, line.Name, line.Name, line.UnitPrice, line.VatRate);

            for (int piece = 1; piece <= line.Quantity; piece++)
            {
                vouchers.Add(Voucher.Issue(
                    integrationEvent.EnterpriseId,
                    Voucher.NewCode(options.CodePrefix),
                    integrationEvent.OrderId,
                    order.Number,
                    product,
                    order.Language,
                    piece,
                    options.ValidFrom,
                    options.ValidTo,
                    now));
            }
        }

        outbox.Publish(new VouchersIssuedIntegrationEvent(
            Guid.CreateVersion7(), now, integrationEvent.EnterpriseId, integrationEvent.OrderId, integrationEvent.AccessToken));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class VoucherDirectory(IVoucherRepository vouchers) : IVoucherDirectory
{
    public async Task<IReadOnlyList<VoucherDto>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        [.. (await vouchers.GetByOrderAsync(orderId, cancellationToken).ConfigureAwait(false))
            .Select(v => new VoucherDto(
                v.Code, v.OrderNumber, v.ProductCode, v.Name, v.DisplayName, v.Language, v.Price, v.Piece,
                v.ValidFrom, v.ValidTo, v.Status.ToString()))];
}
