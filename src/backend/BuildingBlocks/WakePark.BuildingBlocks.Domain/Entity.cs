namespace WakePark.BuildingBlocks.Domain;

/// <summary>
/// Základ pro entity s identitou. Rovnost se určuje podle <see cref="Id"/>, nikoli podle hodnot atributů.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id entity nesmí být prázdné.", nameof(id));
        }

        Id = id;
    }

    /// <summary>Konstruktor pro EF Core. Nepoužívat v aplikačním kódu.</summary>
    protected Entity()
    {
    }

    public Guid Id { get; private init; }

    /// <summary>Nové Id pro entitu. UUID v7 je sekvenční, takže neroztrhá index primárního klíče.</summary>
    protected static Guid NewId() => Guid.CreateVersion7();

    public bool Equals(Entity? other) =>
        other is not null && GetType() == other.GetType() && Id == other.Id;

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
