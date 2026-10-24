namespace WakePark.Modules.Catalog.Application;

/// <summary>Nastavení katalogu (sekce <c>Catalog</c>).</summary>
public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>
    /// Časové pásmo, ve kterém se počítá „dnes“ pro platnost cen. Cena od 1. 5. platí od
    /// půlnoci v Praze, ne v UTC.
    /// </summary>
    public string TimeZoneId { get; init; } = "Europe/Prague";
}
