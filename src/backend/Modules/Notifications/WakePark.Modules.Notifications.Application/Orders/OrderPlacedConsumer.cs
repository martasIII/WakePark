using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.Application.Orders;

/// <summary>
/// Objednávka vznikla → do fronty potvrzovací e-mail s platebními údaji a QR kódem.
/// Idempotentní: opakované doručení stejné události e-mail nezdvojí.
/// </summary>
internal sealed class OrderPlacedConsumer(
    IOrderDirectory orders,
    IEmailRepository emails,
    INotificationsUnitOfWork unitOfWork,
    IQrCodeImage qrCodes,
    NotificationsOptions options)
    : IIntegrationEventConsumer<OrderPlacedIntegrationEvent>,
      IIntegrationEventConsumer<OrderPaymentRemindedIntegrationEvent>
{
    public Task HandleAsync(OrderPlacedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return QueueAsync(integrationEvent.EventId, integrationEvent.OrderId, integrationEvent.AccessToken, reminder: false, cancellationToken);
    }

    /// <summary>Upomínka = stejný e-mail s QR a platebními údaji, jiný předmět a úvod.</summary>
    public Task HandleAsync(OrderPaymentRemindedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return QueueAsync(integrationEvent.EventId, integrationEvent.OrderId, integrationEvent.AccessToken, reminder: true, cancellationToken);
    }

    private async Task QueueAsync(Guid eventId, Guid orderId, string accessToken, bool reminder, CancellationToken cancellationToken)
    {
        string kind = reminder ? OrderPlacedEmail.ReminderKind : OrderPlacedEmail.Kind;
        if (await emails.ExistsAsync(eventId, kind, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Objednávka musí existovat — vznikla ve stejné transakci jako událost. Když ji nejde
        // přečíst, je to chyba a outbox to zkusí znovu.
        OrderDto order = await orders.GetOrderAsync(accessToken, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Objednávka {orderId} z události neexistuje.");

        // Než upomínka odejde, mohla dorazit platba — pak se neposílá.
        if (reminder && order.Payment is null)
        {
            return;
        }

        (string subject, string html, string text) = OrderPlacedEmail.Render(
            order,
            OrderUrl(order, accessToken),
            options,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"),
            reminder);

        List<EmailAttachment> attachments = order.Payment is { } payment
            ? [new EmailAttachment($"qr-platba-{order.Number}.png", "image/png", qrCodes.Png(payment.Spayd), OrderPlacedEmail.QrContentId)]
            : [];

        emails.Add(EmailMessage.Create(eventId, kind, order.CustomerEmail, subject, html, text, attachments));

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Odkaz na stránku objednávky ve webu, v jazyce zákazníka (čeština bez prefixu).</summary>
    private Uri OrderUrl(OrderDto order, string accessToken) =>
        new(options.PublicBaseUrl, order.Language == "cs" ? $"/objednavka/{accessToken}" : $"/{order.Language}/objednavka/{accessToken}");
}
