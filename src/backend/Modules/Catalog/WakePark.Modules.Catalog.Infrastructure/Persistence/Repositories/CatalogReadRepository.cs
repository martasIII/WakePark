using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Repositories;

internal sealed class CatalogReadRepository(CatalogDbContext dbContext) : ICatalogReadRepository, ICatalogReader
{
    public async Task<CatalogDto> GetPublishedCatalogAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default)
    {
        // Produkt bez ceny v aktivním ceníku se nenabízí — nešel by koupit.
        var rows = await (
                from product in dbContext.Products.AsNoTracking()
                join category in dbContext.Categories.AsNoTracking() on product.CategoryId equals category.Id
                join item in dbContext.PriceItems.AsNoTracking() on product.Id equals item.ProductId
                join priceList in dbContext.PriceLists.AsNoTracking() on item.PriceListId equals priceList.Id
                where product.EnterpriseId == enterpriseId && product.IsActive && priceList.IsActive
                orderby category.SortOrder, product.SortOrder
                select new { product, category, item.UnitPrice, item.VatRate, priceList.Currency })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<CatalogCategoryDto> categories =
        [
            .. rows
                .Select(r => r.category)
                .DistinctBy(c => c.Id)
                .Select(c => new CatalogCategoryDto(c.Code, c.Name.Translations)),
        ];

        List<CatalogProductDto> products =
        [
            .. rows.Select(r => new CatalogProductDto(
                r.product.Id,
                r.product.Code,
                r.category.Code,
                r.product.Name.Translations,
                r.product.Description.Translations,
                r.product.ImagePath,
                r.UnitPrice,
                r.Currency,
                r.VatRate)),
        ];

        return new CatalogDto(categories, products);
    }
}
