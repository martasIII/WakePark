namespace WakePark.Modules.Operations.Contracts;

// Provozní dny, blokace a vleky (ADR 0009). Data a časy dní jsou v lokálním čase areálu
// (TimeZoneId), okamžiky blokací v UTC.

/// <summary>Otevírací doba na veřejném webu: dny od dneška dopředu podle nastavení areálu.</summary>
public sealed record PublicOpeningHoursDto(string TimeZoneId, IReadOnlyList<PublicOpeningDayDto> Days);

/// <summary>Den na webu. Bez časů = zavřeno. Blokace jen s důvodem — interní poznámka se na web nedostane.</summary>
public sealed record PublicOpeningDayDto(
    DateOnly Date,
    TimeOnly? OpensAt,
    TimeOnly? ClosesAt,
    IReadOnlyList<PublicBlockingDto> Blockings);

/// <param name="CableName">Null = celý areál.</param>
/// <param name="From">Začátek v lokálním čase areálu (v rámci dne).</param>
/// <param name="To">Konec v lokálním čase areálu.</param>
/// <param name="Category">TechnicalFault, PrivateEvent, Weather, Other.</param>
public sealed record PublicBlockingDto(string? CableName, TimeOnly From, TimeOnly To, string Category);

/// <summary>Provozní kalendář pro backoffice: otevřené dny a blokace v rozsahu.</summary>
public sealed record OperatingCalendarDto(
    string TimeZoneId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<OperatingDayDto> Days,
    IReadOnlyList<BlockingDto> Blockings);

public sealed record OperatingDayDto(DateOnly Date, TimeOnly OpensAt, TimeOnly ClosesAt);

public sealed record BlockingDto(
    Guid Id,
    Guid? CableId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Category,
    string? Note,
    string? CreatedBy,
    DateTimeOffset CreatedAtUtc);

/// <summary>Otevírací doba jednoho dne. Oba časy null = ten den zavřeno.</summary>
public sealed record SetOperatingDayRequest(TimeOnly? OpensAt, TimeOnly? ClosesAt);

/// <summary>
/// Nová blokace. Začátek a konec v lokálním čase areálu (tak je zadává obsluha);
/// bez vleku = celý areál. Kategorie: TechnicalFault, PrivateEvent, Weather, Other.
/// </summary>
public sealed record CreateBlockingRequest(Guid? CableId, DateTime Start, DateTime End, string Category, string? Note);

/// <summary>CSV s provozní dobou: řádky <c>datum;od;do</c>.</summary>
public sealed record OperatingDaysImportRequest(string Csv);

/// <summary>
/// Co import udělá (náhled), nebo co udělal. S chybami se nic neuloží.
/// <c>Change</c>: Added, Changed, Closed, Unchanged.
/// </summary>
public sealed record OperatingDaysImportDto(
    IReadOnlyList<string> Errors,
    IReadOnlyList<OperatingDayDiffDto> Days,
    int Added,
    int Changed,
    int Closed,
    int Unchanged);

public sealed record OperatingDayDiffDto(
    DateOnly Date,
    string Change,
    TimeOnly? OldOpensAt,
    TimeOnly? OldClosesAt,
    TimeOnly? NewOpensAt,
    TimeOnly? NewClosesAt);

/// <summary>Vlek s parametry a schématem překážek — pro backoffice a vizualizaci.</summary>
public sealed record CableDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int TowerCount,
    int? LengthMeters,
    IReadOnlyList<ObstacleDto> Obstacles,
    bool IsActive);

public sealed record ObstacleDto(string Name, string Kind, int? PositionMeters, string? Description);
