using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Operations.Domain.Calendar;

/// <summary>Proč se nejezdí. Na webu se ukáže kategorie, poznámka nikdy.</summary>
public enum BlockingCategory
{
    TechnicalFault,
    PrivateEvent,
    Weather,
    Other,
}

/// <summary>
/// Blokace vleku nebo celého areálu na čas od–do: technická závada, firemní akce, vichřice
/// (ADR 0009). Zadává ji i obsluha z mobilu. V blokovaném čase se nedá rezervovat.
/// </summary>
/// <remarks>
/// Rezervace, které už v tom čase jsou, blokace neruší — obsluha je vyřeší sama (hromadný
/// e-mail, nebo obvolá). Napojení přijde s rezervacemi.
/// </remarks>
public sealed class Blocking : AggregateRoot, IAuditable
{
    public const int NoteMaxLength = 500;

    private Blocking()
    {
    }

    private Blocking(Guid id, Guid wakeParkId, Guid? cableId, DateTimeOffset startUtc, DateTimeOffset endUtc, BlockingCategory category, string? note)
        : base(id)
    {
        WakeParkId = wakeParkId;
        CableId = cableId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Category = category;
        Note = note;
    }

    public Guid WakeParkId { get; private set; }

    /// <summary>Null = celý areál.</summary>
    public Guid? CableId { get; private set; }

    public DateTimeOffset StartUtc { get; private set; }

    /// <summary>Konec, exkluzivně.</summary>
    public DateTimeOffset EndUtc { get; private set; }

    public BlockingCategory Category { get; private set; }

    /// <summary>Interní poznámka pro obsluhu. Na web nikdy.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Blocking Create(
        Guid wakeParkId,
        Guid? cableId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        BlockingCategory category,
        string? note)
    {
        if (wakeParkId == Guid.Empty)
        {
            throw new DomainException("blocking.park_required", "Blokace musí patřit areálu.");
        }

        if (endUtc <= startUtc)
        {
            throw new DomainException("blocking.invalid_range", "Konec blokace musí být po jejím začátku.");
        }

        if (endUtc - startUtc > TimeSpan.FromDays(31))
        {
            throw new DomainException("blocking.too_long", "Blokace smí trvat nejvýš měsíc. Delší zavírka = vynechané provozní dny.");
        }

        if (!Enum.IsDefined(category))
        {
            throw new DomainException("blocking.invalid_category", "Neznámý důvod blokace.");
        }

        string? cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (cleanNote is { Length: > NoteMaxLength })
        {
            throw new DomainException("blocking.note_too_long", "Poznámka je příliš dlouhá.");
        }

        return new Blocking(NewId(), wakeParkId, cableId, startUtc.ToUniversalTime(), endUtc.ToUniversalTime(), category, cleanNote);
    }

    /// <summary>Týká se blokace daného vleku? Blokace celého areálu se týká všech.</summary>
    public bool AppliesTo(Guid cableId) => CableId is null || CableId == cableId;

    /// <summary>Překrývá se s intervalem [start, end)?</summary>
    public bool Overlaps(DateTimeOffset start, DateTimeOffset end) => start < EndUtc && StartUtc < end;
}
