using WakePark.BuildingBlocks.Application;
using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Application.Catalog;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Common;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.Application.Admin;

/// <summary>
/// Katalog přihlášeného provozovatele načtený pro úpravu. Katalog je malý, takže každý příkaz
/// admina pracuje s celým — pravidla napříč záznamy (unikátní kód, strom kategorií, e-shop jen
/// s kladnou cenou) se pak hlídají bez dalších dotazů.
/// </summary>
/// <remarks>Data jsou omezená na provozovatele z přihlášení (ADR 0007).</remarks>
internal sealed class CatalogWorkspace
{
    public const string DefaultPriceListName = "Ceník";
    public const string DefaultCurrency = "CZK";

    private readonly ICatalogRepository _repository;
    private readonly ICatalogUnitOfWork _unitOfWork;
    private readonly List<ProductCategory> _categories;
    private readonly List<Product> _products;

    private CatalogWorkspace(
        ICatalogRepository repository,
        ICatalogUnitOfWork unitOfWork,
        Guid enterpriseId,
        DateOnly today,
        List<ProductCategory> categories,
        List<Product> products,
        PriceList? priceList)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        EnterpriseId = enterpriseId;
        Today = today;
        _categories = categories;
        _products = products;
        PriceList = priceList;
    }

    public Guid EnterpriseId { get; }

    public DateOnly Today { get; }

    public IReadOnlyList<ProductCategory> Categories => _categories;

    public IReadOnlyList<Product> Products => _products;

    public PriceList? PriceList { get; private set; }

    public static async Task<CatalogWorkspace> LoadAsync(
        ICatalogRepository repository,
        ICatalogUnitOfWork unitOfWork,
        ITenantContext tenant,
        CatalogCalendar calendar,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(calendar);

        // Bez provozovatele nic — uživatel bez EnterpriseId nemá co upravovat (ADR 0007).
        Guid enterpriseId = tenant.EnterpriseId ?? throw new NotFoundException("Katalog nenalezen.");

        IReadOnlyList<ProductCategory> categories = await repository.GetCategoriesAsync(enterpriseId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Product> products = await repository.GetProductsAsync(enterpriseId, cancellationToken).ConfigureAwait(false);
        PriceList? priceList = await repository.GetActivePriceListAsync(enterpriseId, cancellationToken).ConfigureAwait(false);

        return new CatalogWorkspace(
            repository, unitOfWork, enterpriseId, calendar.Today, [.. categories], [.. products], priceList);
    }

    public ProductCategory Category(Guid id) =>
        _categories.Find(c => c.Id == id) ?? throw new NotFoundException("Kategorie nenalezena.");

    public Product Product(Guid id) =>
        _products.Find(p => p.Id == id) ?? throw new NotFoundException("Produkt nenalezen.");

    public void Add(ProductCategory category)
    {
        if (_categories.Exists(c => c.Code == category.Code))
        {
            throw new DomainException("category.duplicate_code", $"Kategorie s kódem '{category.Code}' už existuje.");
        }

        _categories.Add(category);
        _repository.Add(category);
    }

    public void Remove(ProductCategory category)
    {
        if (_products.Exists(p => p.CategoryId == category.Id))
        {
            throw new DomainException("category.not_empty", "Kategorie obsahuje produkty — přesuňte je nebo skryjte kategorii jinak.");
        }

        if (_categories.Exists(c => c.ParentId == category.Id))
        {
            throw new DomainException("category.has_children", "Kategorie má podkategorie — nejdřív je přesuňte nebo smažte.");
        }

        _categories.Remove(category);
        _repository.Remove(category);
    }

    public void Add(Product product)
    {
        if (_products.Exists(p => p.Code == product.Code))
        {
            throw new DomainException("product.duplicate_code", $"Produkt s kódem '{product.Code}' už existuje.");
        }

        _products.Add(product);
        _repository.Add(product);
    }

    /// <summary>Aktivní ceník; první cena nebo titulek ho založí.</summary>
    public PriceList EnsurePriceList()
    {
        if (PriceList is null)
        {
            PriceList = Domain.Pricing.PriceList.Create(EnterpriseId, DefaultPriceListName, DefaultCurrency, Today);
            _repository.Add(PriceList);
        }

        return PriceList;
    }

    /// <summary>Kategorie produktu musí patřit stejnému provozovateli.</summary>
    public Guid RequireCategory(Guid categoryId) =>
        _categories.Exists(c => c.Id == categoryId)
            ? categoryId
            : throw new DomainException("product.category_not_found", "Kategorie produktu neexistuje.");

    /// <summary>
    /// Produkt v e-shopu musí mít kladnou cenu — dnešní i všechny budoucí. Za nulu by e-shop
    /// prodával zadarmo; ceník a pokladna nulu snesou (helma zdarma).
    /// </summary>
    public void EnsureEshopPricesArePositive(Product product)
    {
        if (!product.IsOfferedIn(ProductChannels.Eshop) || PriceList is null)
        {
            return;
        }

        bool hasZero = PriceList.PriceOn(product.Id, Today) is { UnitPrice: 0m }
            || PriceList.ScheduledPrices(product.Id, Today).Any(p => p.UnitPrice == 0m);

        if (hasZero)
        {
            throw new DomainException("product.eshop_requires_price", "Produkt v e-shopu musí mít kladnou cenu.");
        }
    }

    public async Task<CatalogAdminDto> SaveAsync(CancellationToken cancellationToken)
    {
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return View();
    }

    public CatalogAdminDto View() => CatalogViews.Admin(new CatalogSnapshot(_categories, _products, PriceList), Today);

    public static LocalizedText Text(IReadOnlyDictionary<string, string> translations) => LocalizedText.Create(translations);

    /// <summary>Nepovinný text: chybějící nebo bez češtiny = žádný.</summary>
    public static LocalizedText? OptionalText(IReadOnlyDictionary<string, string>? translations) =>
        translations is null
        || !translations.TryGetValue(LocalizedText.RequiredLanguage, out string? czech)
        || string.IsNullOrWhiteSpace(czech)
            ? null
            : LocalizedText.Create(translations);

    public static ProductChannels Channels(ProductVisibilityDto visibility)
    {
        ArgumentNullException.ThrowIfNull(visibility);

        return (visibility.PriceList ? ProductChannels.PriceList : ProductChannels.None)
            | (visibility.Eshop ? ProductChannels.Eshop : ProductChannels.None)
            | (visibility.Pos ? ProductChannels.Pos : ProductChannels.None);
    }
}

/// <summary>Načte <see cref="CatalogWorkspace"/> přihlášeného provozovatele.</summary>
internal sealed class CatalogWorkspaceLoader(
    ICatalogRepository repository,
    ICatalogUnitOfWork unitOfWork,
    ITenantContext tenant,
    CatalogCalendar calendar)
{
    public Task<CatalogWorkspace> LoadAsync(CancellationToken cancellationToken) =>
        CatalogWorkspace.LoadAsync(repository, unitOfWork, tenant, calendar, cancellationToken);
}
