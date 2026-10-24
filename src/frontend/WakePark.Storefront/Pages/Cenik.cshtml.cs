using Microsoft.AspNetCore.Mvc.RazorPages;

using WakePark.Storefront.Eshop;

namespace WakePark.Storefront.Pages;

/// <summary>Ceník z katalogu v API (ADR 0009) — stejná data, ze kterých prodává e-shop.</summary>
public sealed class CenikModel(CatalogClient catalogClient) : PageModel
{
    /// <summary>Null, když se ceník nepodařilo načíst — stránka pak ukáže telefon.</summary>
    public WebPriceList? PriceList { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        WebPriceList? priceList = await catalogClient.GetPriceListAsync(cancellationToken).ConfigureAwait(false);

        // Prázdný ceník (nic v katalogu) by byla stránka jen s nadpisem — raději telefon.
        PriceList = priceList is { Sections.Count: > 0 } ? priceList : null;
    }
}
