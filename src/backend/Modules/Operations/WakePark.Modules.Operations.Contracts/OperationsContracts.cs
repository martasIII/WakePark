namespace WakePark.Modules.Operations.Contracts;

/// <summary>Vlek tak, jak ho vidí okolní moduly.</summary>
public sealed record CableDto(
    Guid Id,
    Guid WakeParkId,
    string Name,
    string? Description,
    int TowerCount,
    bool IsActive,
    string TimeZoneId);

/// <summary>
/// Předpis jednoho slotu vyplývající z provozní doby. Není to rezervace ani řádek v databázi —
/// je to výsledek výpočtu „kdy by se dalo jezdit".
/// </summary>
/// <param name="CableId">Vlek, kterého se předpis týká.</param>
/// <param name="StartUtc">Začátek bloku v UTC.</param>
/// <param name="DurationMinutes">Délka bloku v minutách.</param>
/// <param name="Capacity">Kolik jezdců se v bloku vystřídá.</param>
public sealed record SlotTemplateDto(
    Guid CableId,
    DateTimeOffset StartUtc,
    int DurationMinutes,
    int Capacity);

/// <summary>
/// Rozvrh vleku pro okolní moduly. Modul Reservations si přes tohle zjistí, jaké sloty
/// vůbec smí vzniknout — nesahá přitom na entity modulu Operations.
/// </summary>
public interface IOperationsSchedule
{
    /// <summary>Předpisy slotů pro daný den v lokálním čase areálu.</summary>
    Task<IReadOnlyList<SlotTemplateDto>> GetSlotTemplatesAsync(
        Guid cableId,
        DateOnly localDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ověří, že v daném okamžiku podle rozvrhu slot skutečně existuje.
    /// Vrací <c>null</c>, pokud se v ten čas nejezdí.
    /// </summary>
    Task<SlotTemplateDto?> FindSlotTemplateAsync(
        Guid cableId,
        DateTimeOffset startUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Čtení vleků pro ostatní moduly. Jediná cesta, jak se odjinud dostat k údajům o vleku.
/// </summary>
public interface ICableDirectory
{
    Task<CableDto?> GetCableAsync(Guid cableId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CableDto>> GetCablesAsync(Guid wakeParkId, CancellationToken cancellationToken = default);
}
