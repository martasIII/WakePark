using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Orders;

namespace WakePark.Modules.Billing.Infrastructure.Orders;

/// <summary>Každých 15 minut upomínky a zrušení nezaplacených objednávek.</summary>
internal sealed partial class UnpaidOrdersWorker(IServiceScopeFactory scopes, ILogger<UnpaidOrdersWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);
        do
        {
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                UnpaidOrdersResult result = await scope.ServiceProvider
                    .GetRequiredService<ICommandHandler<ProcessUnpaidOrdersCommand, UnpaidOrdersResult>>()
                    .HandleAsync(new ProcessUnpaidOrdersCommand(), stoppingToken)
                    .ConfigureAwait(false);

                if (result is not { Reminded: 0, Cancelled: 0 })
                {
                    LogProcessed(logger, result.Reminded, result.Cancelled);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(EventId = 3101, Level = LogLevel.Information, Message = "Nezaplacené objednávky: upomínek {Reminded}, zrušeno {Cancelled}.")]
    private static partial void LogProcessed(ILogger logger, int reminded, int cancelled);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Error, Message = "Zpracování nezaplacených objednávek selhalo, zkusí se příště.")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
