using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>
/// Celý katalog provozovatele bez sledování změn. Katalog má desítky záznamů, takže je
/// jednodušší a bezpečnější poskládat pohledy v aplikační vrstvě než v SQL.
/// </summary>
internal sealed class CatalogReadRepository(CatalogDbContext dbContext) : ICatalogReadRepository
{
    public async Task<CatalogSnapshot> GetSnapshotAsync(Guid enterpriseId, CancellationToken cancellationToken = default)
    {
        List<ProductCategory> categories = await dbContext.Categories.AsNoTracking()
            .Where(c => c.EnterpriseId == enterpriseId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Product> products = await dbContext.Products.AsNoTracking()
            .Where(p => p.EnterpriseId == enterpriseId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        PriceList? priceList = await dbContext.PriceLists.AsNoTracking()
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.EnterpriseId == enterpriseId && p.IsActive, cancellationToken)
            .ConfigureAwait(false);

        return new CatalogSnapshot(categories, products, priceList);
    }
}
