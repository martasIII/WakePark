using System.Text.RegularExpressions;

using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Operations.Domain.Cables;

/// <summary>
/// Vlek. U dvousloupu je na laně v jednu chvíli vždy jen jeden jezdec — víc lidí v jednom
/// bloku znamená, že se střídají. Kapacita proto není vlastnost vleku, ale nastavení areálu.
/// </summary>
/// <remarks>
/// Vleky Wake Parku jsou pro rezervace shodné; liší se délkou dráhy a překážkami, což je
/// jen popis a vizualizace (ADR 0009). Spravují se zatím přes <c>provoz.json</c>.
/// </remarks>
public sealed partial class Cable : AggregateRoot, IAuditable
{
    public const int MaxObstacles = 50;

    private readonly List<Obstacle> _obstacles = [];

    private Cable()
    {
    }

    private Cable(Guid id, Guid wakeParkId, string code)
        : base(id)
    {
        WakeParkId = wakeParkId;
        Code = code;
        IsActive = true;
    }

    public Guid WakeParkId { get; private set; }

    /// <summary>Stálý kód v rámci areálu (<c>vlek-1</c>); podle něj se páruje s výchozími daty.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Počet sloupů. 2 pro dvousloup, 5 a víc pro velký okruh.</summary>
    public int TowerCount { get; private set; }

    /// <summary>Délka dráhy v metrech, pokud je známá.</summary>
    public int? LengthMeters { get; private set; }

    /// <summary>Překážky na dráze v pořadí, jak po sobě jdou.</summary>
    public IReadOnlyList<Obstacle> Obstacles => _obstacles.AsReadOnly();

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Cable Create(Guid wakeParkId, string code, string name, int towerCount)
    {
        if (wakeParkId == Guid.Empty)
        {
            throw new DomainException("cable.park_required", "Vlek musí patřit areálu.");
        }

        string trimmed = (code ?? string.Empty).Trim();
        if (!CodePattern().IsMatch(trimmed))
        {
            throw new DomainException("cable.invalid_code", $"Kód vleku '{code}' smí mít jen malá písmena bez diakritiky, číslice a pomlčky.");
        }

        Cable cable = new(NewId(), wakeParkId, trimmed);
        cable.Update(name, towerCount, null, null, []);
        return cable;
    }

    /// <summary>Název, parametry a schéma překážek.</summary>
    public void Update(string name, int towerCount, int? lengthMeters, string? description, IReadOnlyList<Obstacle> obstacles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(obstacles);

        if (towerCount < 2)
        {
            throw new DomainException("cable.invalid_tower_count", "Vlek musí mít aspoň dva sloupy.");
        }

        if (lengthMeters is <= 0 or > 5000)
        {
            throw new DomainException("cable.invalid_length", "Délka dráhy musí být kladná (v metrech).");
        }

        if (obstacles.Count > MaxObstacles)
        {
            throw new DomainException("cable.too_many_obstacles", $"Vlek smí mít nejvýš {MaxObstacles} překážek.");
        }

        if (lengthMeters is { } length && obstacles.Any(o => o.PositionMeters > length))
        {
            throw new DomainException("cable.obstacle_outside", "Překážka nesmí být za koncem dráhy.");
        }

        Name = name.Trim();
        TowerCount = towerCount;
        LengthMeters = lengthMeters;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        _obstacles.Clear();
        _obstacles.AddRange(obstacles);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}

/// <summary>
/// Překážka na dráze — kicker, rail, box … <see cref="Kind"/> je volný text, druhů překážek je
/// víc, než by šlo rozumně vyjmenovat. Poloha je od startu v metrech, pokud je známá.
/// </summary>
public sealed record Obstacle
{
    public Obstacle(string name, string kind, int? positionMeters, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        if (name.Trim().Length > 100 || kind.Trim().Length > 50 || description?.Trim().Length > 500)
        {
            throw new DomainException("cable.obstacle_too_long", "Název, druh nebo popis překážky je příliš dlouhý.");
        }

        if (positionMeters < 0)
        {
            throw new DomainException("cable.obstacle_position", "Poloha překážky nesmí být záporná.");
        }

        Name = name.Trim();
        Kind = kind.Trim();
        PositionMeters = positionMeters;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public string Name { get; }

    public string Kind { get; }

    public int? PositionMeters { get; }

    public string? Description { get; }
}
