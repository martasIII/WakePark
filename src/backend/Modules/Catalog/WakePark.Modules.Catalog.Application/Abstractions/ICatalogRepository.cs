using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.Application.Abstractions;

/// <summary>Zápisová strana katalogu — celý katalog provozovatele najednou, je malý.</summary>
public interface ICatalogRepository
{
    Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(Guid enterpriseId, CancellationToken cancellationToken = default);

    /// <summary>Všechny produkty včetně skrytých — import je musí umět znovu zobrazit.</summary>
    Task<IReadOnlyList<Product>> GetProductsAsync(Guid enterpriseId, CancellationToken cancellationToken = default);

    Task<PriceList?> GetActivePriceListAsync(Guid enterpriseId, CancellationToken cancellationToken = default);

    void Add(ProductCategory category);

    void Add(Product product);

    void Add(PriceList priceList);
}

/// <summary>Čtecí strana pro web.</summary>
public interface ICatalogReadRepository
{
    Task<CatalogDto> GetPublishedCatalogAsync(Guid enterpriseId, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IUnitOfWork" />
/// <remarks>Vlastní rozhraní pro každý modul — sdílené by v jednom DI kontejneru přepsal poslední modul.</remarks>
public interface ICatalogUnitOfWork : IUnitOfWork;
