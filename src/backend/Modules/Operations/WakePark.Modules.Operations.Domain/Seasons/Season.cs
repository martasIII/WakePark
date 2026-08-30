using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Operations.Domain.Seasons;

/// <summary>
/// Období, ve kterém platí určitá provozní doba. Hlavní sezóna, předsezóna, zimní provoz.
/// </summary>
public sealed class Season : AggregateRoot, IAuditable
{
    private Season()
    {
    }

    private Season(Guid id, Guid wakeParkId, Guid? cableId, string name, DateOnly startDate, DateOnly endDate)
        : base(id)
    {
        WakeParkId = wakeParkId;
        CableId = cableId;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
    }

    public Guid WakeParkId { get; private set; }

    /// <summary>Null = sezóna platí pro všechny vleky areálu.</summary>
    public Guid? CableId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    /// <summary>Poslední den sezóny, včetně.</summary>
    public DateOnly EndDate { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Season Create(
        Guid wakeParkId,
        Guid? cableId,
        string name,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (wakeParkId == Guid.Empty)
        {
            throw new DomainException("season.park_required", "Sezóna musí patřit areálu.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (endDate < startDate)
        {
            throw new DomainException("season.invalid_range", "Konec sezóny nesmí předcházet jejímu začátku.");
        }

        return new Season(NewId(), wakeParkId, cableId, name.Trim(), startDate, endDate);
    }

    public bool Covers(DateOnly date) => date >= StartDate && date <= EndDate;
}
