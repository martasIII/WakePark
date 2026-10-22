using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Application.Admin;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.Application.Emails;

/// <summary>Jedno kolo odesílání fronty e-mailů.</summary>
public sealed record SendPendingEmailsCommand(int BatchSize = 20) : ICommand<EmailBatchResult>;

public sealed record EmailBatchResult(int Sent, int Failed, int Expired);

internal sealed class SendPendingEmailsCommandHandler(
    IEmailRepository emails,
    IEmailSender sender,
    INotificationsUnitOfWork unitOfWork,
    IClock clock,
    NotificationsOptions options)
    : ICommandHandler<SendPendingEmailsCommand, EmailBatchResult>
{
    public async Task<EmailBatchResult> HandleAsync(SendPendingEmailsCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        int sent = 0, failed = 0, expired = 0;

        foreach (EmailMessage message in await emails.GetDueAsync(clock.UtcNow, command.BatchSize, cancellationToken).ConfigureAwait(false))
        {
            DateTimeOffset now = clock.UtcNow;

            if (now - message.CreatedAtUtc > options.MaxAge)
            {
                message.Expire(now);
                expired++;
                AlertUndelivered(message, "propadl — do 24 hodin se ho nepodařilo odeslat");
            }
            else
            {
                try
                {
                    await sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
                    message.MarkSent(now);
                    sent++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.MarkFailed($"{ex.GetType().Name}: {ex.Message}", now);
                    failed++;

                    if (message.Status == EmailStatus.Failed)
                    {
                        AlertUndelivered(message, "ani po všech pokusech se ho nepodařilo odeslat");
                    }
                }
            }

            // Po každém e-mailu: kdyby proces spadl, odeslané se znovu neodešlou.
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new EmailBatchResult(sent, failed, expired);
    }

    /// <summary>
    /// Zákazník nedostal e-mail (potvrzení, vouchery) → upozornit provozovatele. Upozornění
    /// na upozornění se neposílá — jinak by výpadek SMTP rozjel nekonečnou smyčku.
    /// </summary>
    private void AlertUndelivered(EmailMessage message, string what)
    {
        if (message.Kind.StartsWith("admin-", StringComparison.Ordinal))
        {
            return;
        }

        new AdminAlerts(emails, unitOfWork, options).Queue(
            Guid.CreateVersion7(),
            AdminAlerts.Kind,
            $"Nedoručený e-mail zákazníkovi: {message.Subject}",
            $"""
            E-mail zákazníkovi {what}. Pošlete mu ho prosím jinak (nebo zkontrolujte adresu).

            Komu:    {message.To}
            Předmět: {message.Subject}
            Druh:    {message.Kind}
            Chyba:   {message.LastError}
            """);
    }
}
