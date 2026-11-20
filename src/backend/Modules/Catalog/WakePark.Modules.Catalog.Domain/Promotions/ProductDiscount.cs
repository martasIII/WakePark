namespace WakePark.Modules.Catalog.Domain.Promotions;

/// <summary>
/// Výjimka z procenta akce pro jeden produkt kategorie. <c>0</c> = produkt v akci není.
/// </summary>
public sealed record ProductDiscount(Guid ProductId, int DiscountPercent);
