namespace WakePark.Modules.Catalog.Contracts;

/// <summary>
/// Nabídka e-shopu: kategorie a aktivní produkty s cenou, seřazené tak, jak se mají zobrazit.
/// Texty jsou slovníky jazyk → text; výběr jazyka je věc klienta.
/// </summary>
public sealed record CatalogDto(
    IReadOnlyList<CatalogCategoryDto> Categories,
    IReadOnlyList<CatalogProductDto> Products);

public sealed record CatalogCategoryDto(
    string Code,
    IReadOnlyDictionary<string, string> Name);

public sealed record CatalogProductDto(
    Guid Id,
    string Code,
    string CategoryCode,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description,
    string? ImagePath,
    decimal UnitPrice,
    string Currency,
    decimal VatRate);

/// <summary>Ceník na webu: sekce (kategorie) s podsekcemi a položkami s dnes platnou cenou.</summary>
public sealed record PublicPriceListDto(
    IReadOnlyDictionary<string, string>? Title,
    string Currency,
    IReadOnlyList<PriceListSectionDto> Sections);

/// <summary>Sekce ceníku. Může být i bez položek, když má popis („Firemní akce — napište nám“).</summary>
public sealed record PriceListSectionDto(
    string Code,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    IReadOnlyList<PriceListItemDto> Items,
    IReadOnlyList<PriceListSectionDto> Subsections);

public sealed record PriceListItemDto(
    string Code,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    IReadOnlyDictionary<string, string>? Badge,
    decimal UnitPrice);
