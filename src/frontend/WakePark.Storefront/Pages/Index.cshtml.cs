using Microsoft.AspNetCore.Mvc.RazorPages;

using WakePark.Storefront.Eshop;

namespace WakePark.Storefront.Pages;

/// <summary>
/// Úvod: dlaždice a nad Wake Shopem páska s názvem akce, když dnes v e-shopu nějaká platí
/// (ADR 0014). Nabídku bere ze stejné minutové paměti jako e-shop, takže si neodporují.
/// </summary>
public sealed class IndexModel(CatalogClient catalogClient) : PageModel
{
    /// <summary>
    /// Úvod je nejnavštěvovanější stránka a dřív na API vůbec nečekal. Když API do sekundy
    /// neodpoví (typicky hned po restartu), ukáže se úvod bez pásky — další návštěvník to zkusí znovu.
    /// </summary>
    public static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Akce pro pásku, nebo null (žádná akce, API neodpovídá, e-shop není nastavený).</summary>
    public EshopPromotion? Promotion { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ApiTimeout);

        try
        {
            EshopCatalog? catalog = await catalogClient.GetCatalogAsync(timeout.Token).ConfigureAwait(false);
            Promotion = catalog?.CurrentPromotion;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // API nestihlo odpovědět — úvod bez pásky.
        }
    }
}
