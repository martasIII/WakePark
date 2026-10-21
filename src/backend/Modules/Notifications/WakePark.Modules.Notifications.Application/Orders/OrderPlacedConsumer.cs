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
    : IIntegrationEventConsumer<OrderPlacedIntegrationEvent>
{
    public async Task HandleAsync(OrderPlacedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (await emails.ExistsAsync(integrationEvent.EventId, OrderPlacedEmail.Kind, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Objednávka musí existovat — vznikla ve stejné transakci jako událost. Když ji nejde
        // přečíst, je to chyba a outbox to zkusí znovu.
        OrderDto order = await orders.GetOrderAsync(integrationEvent.AccessToken, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Objednávka {integrationEvent.OrderId} z události neexistuje.");

        (string subject, string html, string text) = OrderPlacedEmail.Render(
            order,
            OrderUrl(order, integrationEvent.AccessToken),
            options,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"));

        List<EmailAttachment> attachments = order.Payment is { } payment
            ? [new EmailAttachment($"qr-platba-{order.Number}.png", "image/png", qrCodes.Png(payment.Spayd), OrderPlacedEmail.QrContentId)]
            : [];

        emails.Add(EmailMessage.Create(
            integrationEvent.EventId, OrderPlacedEmail.Kind, order.CustomerEmail, subject, html, text, attachments));

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Odkaz na stránku objednávky ve webu, v jazyce zákazníka (čeština bez prefixu).</summary>
    private Uri OrderUrl(OrderDto order, string accessToken) =>
        new(options.PublicBaseUrl, order.Language == "cs" ? $"/objednavka/{accessToken}" : $"/{order.Language}/objednavka/{accessToken}");
}
