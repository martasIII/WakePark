using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.BuildingBlocks.Domain;

/// <summary>
/// Kořen agregátu — jediný vstupní bod pro změnu stavu celého agregátu a jediné místo,
/// odkud se publikují doménové události.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id)
        : base(id)
    {
    }

    /// <summary>Konstruktor pro EF Core. Nepoužívat v aplikačním kódu.</summary>
    protected AggregateRoot()
    {
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
