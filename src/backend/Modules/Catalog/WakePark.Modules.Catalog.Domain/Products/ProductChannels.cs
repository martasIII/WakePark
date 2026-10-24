namespace WakePark.Modules.Catalog.Domain.Products;

/// <summary>
/// Kde se produkt ukazuje (ADR 0009). Jeden katalog pro ceník na webu, e-shop i pokladnu —
/// helma za 0 Kč patří do ceníku a na pokladnu, voucher všude.
/// </summary>
[Flags]
public enum ProductChannels
{
    None = 0,

    /// <summary>Ceník na webu.</summary>
    PriceList = 1,

    /// <summary>E-shop — jen s kladnou cenou.</summary>
    Eshop = 2,

    /// <summary>Pokladna na místě.</summary>
    Pos = 4,

    All = PriceList | Eshop | Pos,
}
