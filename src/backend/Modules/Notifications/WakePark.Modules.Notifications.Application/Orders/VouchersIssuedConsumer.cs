using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Domain.Emails;
using WakePark.Modules.Vouchers.Contracts;

namespace WakePark.Modules.Notifications.Application.Orders;

/// <summary>
/// Poukazy jsou vystavené → e-mail s PDF poukazů a daňového dokladu. Idempotentní stejně
/// jako potvrzení objednávky.
/// </summary>
internal sealed class VouchersIssuedConsumer(
    IOrderDirectory orders,
    IVoucherDirectory vouchers,
    IInvoiceDirectory invoices,
    IEmailRepository emails,
    INotificationsUnitOfWork unitOfWork,
    IDocumentRenderer documents,
    NotificationsOptions options)
    : IIntegrationEventConsumer<VouchersIssuedIntegrationEvent>
{
    public async Task HandleAsync(VouchersIssuedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (await emails.ExistsAsync(integrationEvent.EventId, VouchersIssuedEmail.Kind, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        OrderDto order = await orders.GetOrderAsync(integrationEvent.AccessToken, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Objednávka {integrationEvent.OrderId} z události neexistuje.");

        IReadOnlyList<VoucherDto> issued = await vouchers.GetByOrderAsync(integrationEvent.OrderId, cancellationToken).ConfigureAwait(false);
        if (issued.Count == 0)
        {
            throw new InvalidOperationException($"K objednávce {order.Number} nejsou vystavené poukazy.");
        }

        // Doklad vzniká ve stejné transakci jako zaplacení; když chybí, je to chyba a zkusí se znovu.
        InvoiceDto invoice = await invoices.GetByOrderAsync(integrationEvent.OrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"K zaplacené objednávce {order.Number} chybí daňový doklad.");

        List<EmailAttachment> attachments =
        [
            .. issued.Select(v => new EmailAttachment($"voucher-{v.Code}.pdf", "application/pdf", documents.VoucherPdf(v))),
            new EmailAttachment($"doklad-{invoice.Number}.pdf", "application/pdf", documents.InvoicePdf(invoice)),
        ];

        Uri orderUrl = new(options.PublicBaseUrl, order.Language == "cs"
            ? $"/objednavka/{integrationEvent.AccessToken}"
            : $"/{order.Language}/objednavka/{integrationEvent.AccessToken}");

        (string subject, string html, string text) = VouchersIssuedEmail.Render(order, issued, invoice, orderUrl, options);

        emails.Add(EmailMessage.Create(
            integrationEvent.EventId, VouchersIssuedEmail.Kind, order.CustomerEmail, subject, html, text, attachments));

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
