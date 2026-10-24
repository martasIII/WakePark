using NSubstitute;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Catalog.Application;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.UnitTests.Application;

/// <summary>
/// Repozitář v paměti — mezi dvěma voláními „uloží“ tím, že objekty drží. Slouží zároveň
/// jako čtecí strana, takže pohledy vidí totéž, co zapsaly příkazy.
/// </summary>
internal sealed class InMemoryCatalogRepository : ICatalogRepository, ICatalogReadRepository
{
    public List<ProductCategory> Categories { get; } = [];

    public List<Product> Products { get; } = [];

    public List<PriceList> PriceLists { get; } = [];

    public Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(Guid enterpriseId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductCategory>>([.. Categories.Where(c => c.EnterpriseId == enterpriseId)]);

    public Task<IReadOnlyList<Product>> GetProductsAsync(Guid enterpriseId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Product>>([.. Products.Where(p => p.EnterpriseId == enterpriseId)]);

    public Task<PriceList?> GetActivePriceListAsync(Guid enterpriseId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PriceLists.Find(p => p.EnterpriseId == enterpriseId && p.IsActive));

    public async Task<CatalogSnapshot> GetSnapshotAsync(Guid enterpriseId, CancellationToken cancellationToken = default) =>
        new(
            await GetCategoriesAsync(enterpriseId, cancellationToken),
            await GetProductsAsync(enterpriseId, cancellationToken),
            await GetActivePriceListAsync(enterpriseId, cancellationToken));

    public void Add(ProductCategory category) => Categories.Add(category);

    public void Add(Product product) => Products.Add(product);

    public void Add(PriceList priceList) => PriceLists.Add(priceList);

    public void Remove(ProductCategory category) => Categories.Remove(category);
}

/// <summary>Hodiny pro testy, které se dají posunout.</summary>
internal sealed class TestCalendar
{
    public TestCalendar(DateTimeOffset now)
    {
        Clock.UtcNow.Returns(now);
        Calendar = new CatalogCalendar(Clock, new CatalogOptions());
    }

    public IClock Clock { get; } = Substitute.For<IClock>();

    public CatalogCalendar Calendar { get; }

    public void Set(DateTimeOffset now) => Clock.UtcNow.Returns(now);
}
