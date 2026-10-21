using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Notifications.Application.Emails;

namespace WakePark.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Každých 15 s odešle e-maily z fronty. Běží jen se zapnutým odesíláním
/// (<c>Notifications:Enabled</c>) — do té doby se e-maily jen připravují.
/// </summary>
internal sealed partial class EmailSenderWorker(IServiceScopeFactory scopes, ILogger<EmailSenderWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);

        using PeriodicTimer timer = new(Interval);
        do
        {
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                EmailBatchResult result = await scope.ServiceProvider
                    .GetRequiredService<ICommandHandler<SendPendingEmailsCommand, EmailBatchResult>>()
                    .HandleAsync(new SendPendingEmailsCommand(), stoppingToken)
                    .ConfigureAwait(false);

                if (result is not { Sent: 0, Failed: 0, Expired: 0 })
                {
                    LogBatch(logger, result.Sent, result.Failed, result.Expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Odesílání e-mailů běží.")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "E-maily: odesláno {Sent}, neúspěšně {Failed}, propadlo {Expired}.")]
    private static partial void LogBatch(ILogger logger, int sent, int failed, int expired);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Error, Message = "Odesílání e-mailů selhalo, zkusí se příště.")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
