using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Repositories;

internal sealed class CatalogRepository(CatalogDbContext dbContext) : ICatalogRepository
{
    public async Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Categories
            .Where(c => c.EnterpriseId == enterpriseId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Product>> GetProductsAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Products
            .Where(p => p.EnterpriseId == enterpriseId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<PriceList?> GetActivePriceListAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default) =>
        dbContext.PriceLists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.EnterpriseId == enterpriseId && p.IsActive, cancellationToken);

    public void Add(ProductCategory category) => dbContext.Categories.Add(category);

    public void Add(Product product) => dbContext.Products.Add(product);

    public void Add(PriceList priceList) => dbContext.PriceLists.Add(priceList);
}
