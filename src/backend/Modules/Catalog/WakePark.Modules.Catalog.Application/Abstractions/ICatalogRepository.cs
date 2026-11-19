using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;
using WakePark.Modules.Catalog.Domain.Promotions;

namespace WakePark.Modules.Catalog.Application.Abstractions;

/// <summary>Zápisová strana katalogu — celý katalog provozovatele najednou, je malý.</summary>
public interface ICatalogRepository
{
    Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(Guid enterpriseId, CancellationToken cancellationToken = default);

    /// <summary>Všechny produkty včetně skrytých — import je musí umět znovu zobrazit.</summary>
    Task<IReadOnlyList<Product>> GetProductsAsync(Guid enterpriseId, CancellationToken cancellationToken = default);

    Task<PriceList?> GetActivePriceListAsync(Guid enterpriseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Promotion>> GetPromotionsAsync(Guid enterpriseId, CancellationToken cancellationToken = default);

    void Add(ProductCategory category);

    void Add(Product product);

    void Add(PriceList priceList);

    void Add(Promotion promotion);

    void Remove(ProductCategory category);

    void Remove(Promotion promotion);
}

/// <summary>Čtecí strana: celý katalog bez sledování změn, z něj se skládají pohledy.</summary>
public interface ICatalogReadRepository
{
    Task<CatalogSnapshot> GetSnapshotAsync(Guid enterpriseId, CancellationToken cancellationToken = default);
}

/// <summary>Katalog provozovatele v jednom okamžiku: kategorie, produkty, aktivní ceník a akce.</summary>
public sealed record CatalogSnapshot(
    IReadOnlyList<ProductCategory> Categories,
    IReadOnlyList<Product> Products,
    PriceList? PriceList,
    IReadOnlyList<Promotion> Promotions);

/// <inheritdoc cref="IUnitOfWork" />
/// <remarks>Vlastní rozhraní pro každý modul — sdílené by v jednom DI kontejneru přepsal poslední modul.</remarks>
public interface ICatalogUnitOfWork : IUnitOfWork;
