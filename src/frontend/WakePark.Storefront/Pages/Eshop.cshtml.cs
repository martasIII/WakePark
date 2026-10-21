using Microsoft.AspNetCore.Mvc.RazorPages;

using WakePark.Storefront.Eshop;

namespace WakePark.Storefront.Pages;

public sealed class EshopModel(CatalogClient catalogClient) : PageModel
{
    /// <summary>Null, když API neodpovídá nebo e-shop není nastavený — stránka pak ukáže telefon.</summary>
    public EshopCatalog? Catalog { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Catalog = await catalogClient.GetCatalogAsync(cancellationToken).ConfigureAwait(false);

    public IEnumerable<EshopProduct> ProductsIn(EshopCategory category) =>
        Catalog?.Products.Where(p => p.CategoryCode == category.Code) ?? [];
}
