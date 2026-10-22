using System.Net;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.Application.Orders;

/// <summary>
/// Objednávka se zrušila po splatnosti → krátké oznámení zákazníkovi. Pozdní platbu systém
/// dál přijme, proto to e-mail říká — kdo platbu jen odeslal pozdě, nepřijde o voucher.
/// </summary>
internal sealed class OrderCancelledConsumer(
    IOrderDirectory orders,
    IEmailRepository emails,
    INotificationsUnitOfWork unitOfWork,
    NotificationsOptions options)
    : IIntegrationEventConsumer<OrderCancelledIntegrationEvent>
{
    public const string Kind = "order-cancelled";

    public async Task HandleAsync(OrderCancelledIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (await emails.ExistsAsync(integrationEvent.EventId, Kind, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        OrderDto order = await orders.GetOrderAsync(integrationEvent.AccessToken, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Objednávka {integrationEvent.OrderId} z události neexistuje.");

        // Mezitím zaplaceno (pozdní platba) — zrušení se zákazníkovi neoznamuje.
        if (order.Status == "Paid")
        {
            return;
        }

        bool en = order.Language == "en";
        string subject = en
            ? $"Order {order.Number} cancelled — payment not received"
            : $"Objednávka {order.Number} zrušena — platba nedorazila";
        string text = en
            ? $"""
              Hello {order.CustomerName},

              we have not received the payment for order no. {order.Number}, so the order has been cancelled.
              If you have already sent the payment, don't worry: once it arrives with the right variable symbol, we will e-mail you the voucher.

              Questions? Reply to this e-mail or call {options.Phone}.
              Wake Park Džbán
              """
            : $"""
              Dobrý den, {order.CustomerName},

              platba za objednávku č. {order.Number} k nám nedorazila, a proto jsme objednávku zrušili.
              Pokud jste platbu už odeslali, nic se neděje: jakmile dorazí se správným variabilním symbolem, voucher vám pošleme.

              Dotazy? Odpovězte na tento e-mail nebo volejte {options.Phone}.
              Wake Park Džbán
              """;

        string html = "<!DOCTYPE html><html><body style=\"font-family:Arial,sans-serif;color:#1d1d1f\">"
            + string.Join(string.Empty, text.Split('\n').Select(line => $"<p style=\"margin:0 0 8px\">{WebUtility.HtmlEncode(line)}</p>"))
            + "</body></html>";

        emails.Add(EmailMessage.Create(integrationEvent.EventId, Kind, order.CustomerEmail, subject, html, text));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
