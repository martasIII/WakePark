namespace WakePark.Modules.Catalog.Contracts;

/// <summary>
/// Čtení nabídky pro ostatní moduly (Billing si z ní bere ceny). Jediná cesta, jak se odjinud
/// dostat k produktům a cenám — entity modulu Catalog zůstávají uvnitř.
/// </summary>
public interface ICatalogReader
{
    /// <summary>
    /// Nabídka e-shopu s dnes platnými cenami, stejně jako ji vidí web: aktivní produkty
    /// zobrazované v e-shopu s kladnou cenou.
    /// </summary>
    Task<CatalogDto> GetPublishedCatalogAsync(Guid enterpriseId, CancellationToken cancellationToken = default);
}
