using System.Globalization;
using System.Net;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.Application.Admin;

/// <summary>Upozornění provozovateli = obyčejný e-mail ve frontě, adresát z konfigurace.</summary>
internal sealed class AdminAlerts(IEmailRepository emails, INotificationsUnitOfWork unitOfWork, NotificationsOptions options)
    : IAdminAlerts
{
    public const string Kind = "admin-alert";

    public async Task SendAsync(string subject, string body, CancellationToken cancellationToken = default)
    {
        Queue(Guid.CreateVersion7(), Kind, subject, body);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    internal void Queue(Guid sourceEventId, string kind, string subject, string body)
    {
        string fullSubject = $"[Wake Park e-shop] {subject}";
        if (fullSubject.Length > EmailMessage.SubjectMaxLength)
        {
            fullSubject = fullSubject[..EmailMessage.SubjectMaxLength];
        }

        string html = "<pre style=\"font-family:Consolas,monospace;font-size:13px;white-space:pre-wrap\">"
            + WebUtility.HtmlEncode(body) + "</pre>";

        emails.Add(EmailMessage.Create(sourceEventId, kind, options.AdminEmail, fullSubject, html, body));
    }
}

/// <summary>Platba k ručnímu vyřízení → upozornění na info@. Idempotentní jako ostatní e-maily.</summary>
internal sealed class PaymentNeedsAttentionConsumer(
    IEmailRepository emails,
    INotificationsUnitOfWork unitOfWork,
    NotificationsOptions options)
    : IIntegrationEventConsumer<PaymentNeedsAttentionIntegrationEvent>
{
    public const string Kind = "admin-payment";

    private static readonly CultureInfo Czech = CultureInfo.GetCultureInfo("cs");

    public async Task HandleAsync(PaymentNeedsAttentionIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (await emails.ExistsAsync(integrationEvent.EventId, Kind, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        PaymentNeedsAttentionIntegrationEvent e = integrationEvent;
        string amount = $"{e.Amount.ToString("#,0.00", Czech)} {e.Currency}";
        string body = $"""
            Příchozí platbu se nepodařilo automaticky spárovat s objednávkou. Vyřiďte ji prosím ručně
            (doplatek si vyžádat, nebo platbu vrátit).

            Důvod:      {e.Reason}
            Částka:     {amount}
            VS:         {e.VariableSymbol ?? "(bez VS)"}
            Plátce:     {e.CounterpartyName ?? "(neuveden)"}
            Účet:       {e.CounterpartyAccount ?? "(neuveden)"}
            Zpráva:     {e.Message ?? ""}
            Zaúčtováno: {e.BookedOn.ToString("d. M. yyyy", Czech)}
            """;

        new AdminAlerts(emails, unitOfWork, options).Queue(e.EventId, Kind, $"Platba k ručnímu vyřízení: {amount}, VS {e.VariableSymbol}", body);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
