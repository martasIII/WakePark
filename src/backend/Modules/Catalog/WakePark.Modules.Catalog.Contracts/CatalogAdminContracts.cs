namespace WakePark.Modules.Catalog.Contracts;

// Správa katalogu v backoffice (ADR 0009). Texty jsou slovníky jazyk → text, čeština povinná.

/// <summary>
/// Celý katalog provozovatele pro admina — i skryté produkty a ceny do budoucna.
/// <c>Today</c> je dnešní datum v čase areálu; podle něj se určuje platná cena.
/// </summary>
public sealed record CatalogAdminDto(
    DateOnly Today,
    string Currency,
    IReadOnlyDictionary<string, string>? PriceListTitle,
    IReadOnlyList<AdminCategoryDto> Categories,
    IReadOnlyList<AdminProductDto> Products,
    IReadOnlyList<AdminPromotionDto> Promotions);

/// <summary>
/// Akční sleva (jen e-shop): výchozí procento na kategorii, celé dny od–do včetně.
/// <c>ProductDiscounts</c> = produkty s jiným procentem (0 = bez slevy).
/// </summary>
public sealed record AdminPromotionDto(
    Guid Id,
    IReadOnlyDictionary<string, string> Name,
    Guid CategoryId,
    int DiscountPercent,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    IReadOnlyList<ProductDiscountDto> ProductDiscounts);

/// <summary>Vlastní procento produktu v akci; 0 = produkt v akci není.</summary>
public sealed record ProductDiscountDto(Guid ProductId, int DiscountPercent);

/// <summary>
/// Nová nebo upravená akce. Název česky povinný, anglicky volitelný. Produkty s vlastním
/// procentem musí být přímo v kategorii akce.
/// </summary>
public sealed record PromotionRequest(
    IReadOnlyDictionary<string, string> Name,
    Guid CategoryId,
    int DiscountPercent,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    IReadOnlyList<ProductDiscountDto>? ProductDiscounts = null);

public sealed record AdminCategoryDto(
    Guid Id,
    string Code,
    Guid? ParentId,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    int SortOrder);

public sealed record AdminProductDto(
    Guid Id,
    string Code,
    Guid CategoryId,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    IReadOnlyDictionary<string, string>? Badge,
    string? ImagePath,
    int SortOrder,
    ProductVisibilityDto Visibility,
    bool IsActive,
    PriceDto? CurrentPrice,
    IReadOnlyList<PriceDto> ScheduledPrices);

/// <summary>Kde se produkt ukazuje.</summary>
public sealed record ProductVisibilityDto(bool PriceList, bool Eshop, bool Pos);

/// <summary>Cena včetně DPH, sazba DPH v procentech, platnost od data (čas areálu).</summary>
public sealed record PriceDto(decimal UnitPrice, decimal VatRate, DateOnly ValidFrom);

/// <summary>Nová kategorie. Kód je stálý, později nejde změnit.</summary>
public sealed record CreateCategoryRequest(
    string Code,
    Guid? ParentId,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    int SortOrder);

public sealed record UpdateCategoryRequest(
    Guid? ParentId,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    int SortOrder);

/// <summary>Nový produkt. Kód je stálý; cena se nastaví zvlášť.</summary>
public sealed record CreateProductRequest(
    string Code,
    Guid CategoryId,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    IReadOnlyDictionary<string, string>? Badge,
    string? ImagePath,
    int SortOrder,
    ProductVisibilityDto Visibility);

public sealed record UpdateProductRequest(
    Guid CategoryId,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    IReadOnlyDictionary<string, string>? Badge,
    string? ImagePath,
    int SortOrder,
    ProductVisibilityDto Visibility,
    bool IsActive);

/// <summary>Nová cena. Bez <paramref name="ValidFrom"/> platí od dneška.</summary>
public sealed record SetPriceRequest(decimal UnitPrice, decimal VatRate, DateOnly? ValidFrom);

/// <summary>Podtitulek ceníku na webu („Sezóna 2027“); null ho odebere.</summary>
public sealed record SetPriceListTitleRequest(IReadOnlyDictionary<string, string>? Title);
