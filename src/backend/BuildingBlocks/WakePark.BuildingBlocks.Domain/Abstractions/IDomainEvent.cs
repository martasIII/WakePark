namespace WakePark.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Doménová událost — něco, co se v doméně už stalo. Zpracovává se v rámci stejné transakce.
/// Pro komunikaci mezi moduly slouží integrační události v projektu <c>*.Contracts</c>.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
