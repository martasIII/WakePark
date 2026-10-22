using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Domain.Orders;

namespace WakePark.Modules.Billing.Application.Orders;

/// <summary>
/// Nezaplacené objednávky: po <see cref="BillingOptions.PaymentReminderDays"/> dnech upomínka,
/// po splatnosti zrušení. E-maily pošle Notifications podle událostí z outboxu.
/// Bezpečné pustit kdykoli: upomínka i zrušení proběhnou u objednávky nejvýš jednou.
/// </summary>
public sealed record ProcessUnpaidOrdersCommand : ICommand<UnpaidOrdersResult>;

public sealed record UnpaidOrdersResult(int Reminded, int Cancelled);

internal sealed class ProcessUnpaidOrdersCommandHandler(
    IOrderRepository orders,
    IBillingUnitOfWork unitOfWork,
    IClock clock,
    BillingOptions options)
    : ICommandHandler<ProcessUnpaidOrdersCommand, UnpaidOrdersResult>
{
    public async Task<UnpaidOrdersResult> HandleAsync(ProcessUnpaidOrdersCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DateTimeOffset now = clock.UtcNow;
        int reminded = 0, cancelled = 0;

        foreach (Order order in await orders
                     .GetAwaitingPaymentPlacedBeforeAsync(now.AddDays(-options.PaymentReminderDays), cancellationToken)
                     .ConfigureAwait(false))
        {
            if (order.CancelUnpaid(now))
            {
                cancelled++;
            }
            else if (order.RemindPayment(now))
            {
                reminded++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new UnpaidOrdersResult(reminded, cancelled);
    }
}
