namespace WakePark.Modules.Catalog.Contracts;

/// <summary>
/// Čtení nabídky pro ostatní moduly (Billing si z ní bere ceny). Jediná cesta, jak se odjinud
/// dostat k produktům a cenám — entity modulu Catalog zůstávají uvnitř.
/// </summary>
public interface ICatalogReader
{
    /// <summary>Aktivní produkty s cenou, stejně jako je vidí web.</summary>
    Task<CatalogDto> GetPublishedCatalogAsync(Guid enterpriseId, CancellationToken cancellationToken = default);
}
