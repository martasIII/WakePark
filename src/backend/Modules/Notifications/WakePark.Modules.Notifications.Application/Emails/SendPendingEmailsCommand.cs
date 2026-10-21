using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Notifications.Application.Abstractions;
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
                }
            }

            // Po každém e-mailu: kdyby proces spadl, odeslané se znovu neodešlou.
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new EmailBatchResult(sent, failed, expired);
    }
}
