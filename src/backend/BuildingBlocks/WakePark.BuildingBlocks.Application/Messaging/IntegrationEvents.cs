namespace WakePark.BuildingBlocks.Application.Messaging;

/// <summary>
/// Událost, kterou si moduly posílají mezi sebou (žije v <c>*.Contracts</c> odesílatele).
/// Doručuje ji outbox „aspoň jednou“ — příjemce proto musí být idempotentní a podle
/// <see cref="EventId"/> poznat, že stejnou událost už zpracoval.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAtUtc { get; }
}

/// <summary>Příjemce integrační události v jiném modulu („consumer“, aby se nepletl s handlery příkazů).</summary>
public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}
