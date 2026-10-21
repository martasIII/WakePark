using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Payments;

namespace WakePark.Modules.Billing.Infrastructure.Fio;

/// <summary>
/// Každých pár minut stáhne výpis z Fio a spáruje platby. Běží jen v procesu API (ne při
/// krocích nasazení migrate / import-catalog) a jen s nastaveným tokenem.
/// </summary>
/// <remarks>
/// Chyba jednoho běhu (Fio nedostupné, výpadek databáze) server neshodí — zaloguje se
/// a další běh to zkusí znovu. Díky stahování za několik dní zpětně se nic neztratí.
/// </remarks>
internal sealed partial class BankSyncWorker(
    IServiceScopeFactory scopes,
    IOptions<FioOptions> options,
    IClock clock,
    ILogger<BankSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        FioOptions settings = options.Value;
        LogStarted(logger, settings.Interval);

        // Krátká pauza po startu: nasazení nemá hned bušit do banky a databáze se má rozehřát.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

        using PeriodicTimer timer = new(settings.Interval);
        do
        {
            await SyncOnceAsync(settings, stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task SyncOnceAsync(FioOptions settings, CancellationToken stoppingToken)
    {
        DateOnly today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId)).DateTime);

        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            BankSyncResult result = await scope.ServiceProvider
                .GetRequiredService<ICommandHandler<SyncBankTransactionsCommand, BankSyncResult>>()
                .HandleAsync(
                    new SyncBankTransactionsCommand(settings.EnterpriseId!.Value, today.AddDays(-settings.LookbackDays), today),
                    stoppingToken)
                .ConfigureAwait(false);

            if (result.New > 0)
            {
                LogSynced(logger, result);
            }

            if (result.Unmatched > 0)
            {
                LogUnmatched(logger, result.Unmatched);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(logger, ex);
        }
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Párování plateb z Fio běží, interval {Interval}.")]
    private static partial void LogStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Platby z Fio: {Result}.")]
    private static partial void LogSynced(ILogger logger, BankSyncResult result);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning, Message = "Nespárované platby k ručnímu vyřízení: {Count}.")]
    private static partial void LogUnmatched(ILogger logger, int count);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Error, Message = "Párování plateb z Fio selhalo, zkusí se příště.")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
