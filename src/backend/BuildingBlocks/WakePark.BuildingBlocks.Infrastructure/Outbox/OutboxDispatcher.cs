using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;

namespace WakePark.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Doručuje zprávy z outboxu jednoho modulu příjemcům v ostatních modulech (v rámci procesu).
/// Doručení je „aspoň jednou“: když příjemce spadne, zpráva se zkusí znovu s rostoucím
/// odstupem; po <see cref="OutboxMessage.MaxAttempts"/> pokusech zůstane v tabulce s chybou.
/// </summary>
/// <remarks>
/// Počítá s jednou instancí aplikace. Pro víc instancí by výběr zpráv potřeboval
/// <c>FOR UPDATE SKIP LOCKED</c>, jinak by dvě instance doručily totéž současně.
/// </remarks>
public sealed partial class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopes,
    IClock clock,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private const int BatchSize = 20;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);
        do
        {
            try
            {
                await DispatchPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBatchFailed(logger, typeof(TContext).Name, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>Jedno kolo doručení. Veřejné kvůli testům.</summary>
    /// <returns>Počet úspěšně doručených zpráv.</returns>
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
        DateTimeOffset now = clock.UtcNow;

        List<OutboxMessage> messages = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedAtUtc == null
                && m.Attempts < OutboxMessage.MaxAttempts
                && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now))
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int delivered = 0;
        foreach (OutboxMessage message in messages)
        {
            try
            {
                // Každá zpráva ve vlastním scope: příjemce má čerstvý DbContext a chyba
                // jedné zprávy neovlivní ostatní.
                await using AsyncServiceScope handlerScope = scopes.CreateAsyncScope();
                await DeliverAsync(handlerScope.ServiceProvider, message.Deserialize(), cancellationToken).ConfigureAwait(false);
                message.MarkProcessed(clock.UtcNow);
                delivered++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.MarkFailed($"{ex.GetType().Name}: {ex.Message}", clock.UtcNow);
                LogDeliveryFailed(logger, message.Type, message.Id, message.Attempts, ex);

                if (message.Attempts >= OutboxMessage.MaxAttempts)
                {
                    await AlertGaveUpAsync(message, cancellationToken).ConfigureAwait(false);
                }
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return delivered;
    }

    /// <summary>
    /// Zpráva se po všech pokusech nedoručila — např. k zaplacené objednávce se nevystavil
    /// voucher. Bez upozornění by zákazník zaplatil a nic nedostal a nikdo by to nevěděl.
    /// </summary>
    private async Task AlertGaveUpAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            if (scope.ServiceProvider.GetService<IAdminAlerts>() is { } alerts)
            {
                await alerts.SendAsync(
                    "Vnitřní chyba: zprávu mezi moduly se nepodařilo doručit",
                    $"""
                    Systém to vzdal po {message.Attempts} pokusech. Je potřeba to prověřit ručně.

                    Zpráva: {message.Type}
                    Id: {message.Id}
                    Vznikla: {message.OccurredAtUtc:u}
                    Poslední chyba: {message.LastError}
                    """,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAlertFailed(logger, message.Id, ex);
        }
    }

    private static async Task DeliverAsync(IServiceProvider services, IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        Type handlerType = typeof(IIntegrationEventConsumer<>).MakeGenericType(integrationEvent.GetType());

        foreach (object? handler in services.GetServices(handlerType))
        {
            await ((Task)handlerType
                    .GetMethod(nameof(IIntegrationEventConsumer<IIntegrationEvent>.HandleAsync))!
                    .Invoke(handler, [integrationEvent, cancellationToken])!)
                .ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Doručení {Type} ({MessageId}) selhalo, pokus {Attempt}.")]
    private static partial void LogDeliveryFailed(ILogger logger, string type, Guid messageId, int attempt, Exception ex);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Error, Message = "Upozornění na nedoručitelnou zprávu {MessageId} se nepodařilo odeslat.")]
    private static partial void LogAlertFailed(ILogger logger, Guid messageId, Exception ex);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "Outbox modulu {Context} se nepodařilo zpracovat.")]
    private static partial void LogBatchFailed(ILogger logger, string context, Exception ex);
}
