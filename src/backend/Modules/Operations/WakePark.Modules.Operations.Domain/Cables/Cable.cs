using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Operations.Domain.Cables;

/// <summary>
/// Vlek. U dvousloupu je na laně v jednu chvíli vždy jen jeden jezdec — víc lidí v jednom
/// bloku znamená, že se střídají. Kapacita proto není vlastnost vleku, ale provozní doby.
/// </summary>
public sealed class Cable : AggregateRoot, IAuditable
{
    private Cable()
    {
    }

    private Cable(Guid id, Guid wakeParkId, string name, int towerCount)
        : base(id)
    {
        WakeParkId = wakeParkId;
        Name = name;
        TowerCount = towerCount;
        IsActive = true;
    }

    public Guid WakeParkId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Počet sloupů. 2 pro dvousloup, 5 a víc pro velký okruh.</summary>
    public int TowerCount { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Cable Create(Guid wakeParkId, string name, int towerCount)
    {
        if (wakeParkId == Guid.Empty)
        {
            throw new DomainException("cable.park_required", "Vlek musí patřit areálu.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (towerCount < 2)
        {
            throw new DomainException("cable.invalid_tower_count", "Vlek musí mít aspoň dva sloupy.");
        }

        return new Cable(NewId(), wakeParkId, name.Trim(), towerCount);
    }

    public void Describe(string? description) =>
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
