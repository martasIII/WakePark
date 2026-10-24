using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Contracts;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.Application.Catalog;

/// <summary>
/// Pohledy na jeden katalog: e-shop, ceník na webu a backoffice. Všechny se skládají ze stejného
/// snímku a stejného „dnes“, takže se nemůže stát, že web ukáže jinou cenu, než za jakou
/// e-shop prodá (ADR 0009).
/// </summary>
internal static class CatalogViews
{
    private const string DefaultCurrency = "CZK";

    /// <summary>E-shop: aktivní produkty pro e-shop s dnes platnou kladnou cenou.</summary>
    public static CatalogDto Eshop(CatalogSnapshot snapshot, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.PriceList is not { } priceList)
        {
            return new CatalogDto([], []);
        }

        Dictionary<Guid, ProductCategory> categories = snapshot.Categories.ToDictionary(c => c.Id);

        var offered = snapshot.Products
            .Where(p => p.IsOfferedIn(ProductChannels.Eshop) && categories.ContainsKey(p.CategoryId))
            .Select(p => (Product: p, Price: priceList.PriceOn(p.Id, today)))
            .Where(x => x.Price is { UnitPrice: > 0m })
            .OrderBy(x => SortKey(categories[x.Product.CategoryId], categories), SortKeyComparer.Instance)
            .ThenBy(x => x.Product.SortOrder)
            .ThenBy(x => x.Product.Code, StringComparer.Ordinal)
            .ToList();

        List<CatalogCategoryDto> categoryDtos =
        [
            .. offered
                .Select(x => categories[x.Product.CategoryId])
                .DistinctBy(c => c.Id)
                .Select(c => new CatalogCategoryDto(c.Code, c.Name.Translations)),
        ];

        List<CatalogProductDto> productDtos =
        [
            .. offered.Select(x => new CatalogProductDto(
                x.Product.Id,
                x.Product.Code,
                categories[x.Product.CategoryId].Code,
                x.Product.Name.Translations,
                x.Product.Description?.Translations ?? EmptyText,
                x.Product.ImagePath,
                x.Price!.UnitPrice,
                priceList.Currency,
                x.Price.VatRate)),
        ];

        return new CatalogDto(categoryDtos, productDtos);
    }

    /// <summary>
    /// Ceník na webu: stromem kategorií, položky s dnes platnou cenou (i nulovou — „zdarma“).
    /// Sekce bez položek se ukáže jen s popisem.
    /// </summary>
    public static PublicPriceListDto PriceList(CatalogSnapshot snapshot, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        PriceList? priceList = snapshot.PriceList;
        ILookup<Guid, (Product Product, PriceItem Price)> items = snapshot.Products
            .Where(p => p.IsOfferedIn(ProductChannels.PriceList))
            .Select(p => (Product: p, Price: priceList?.PriceOn(p.Id, today)))
            .Where(x => x.Price is not null)
            .Select(x => (x.Product, Price: x.Price!))
            .ToLookup(x => x.Product.CategoryId);

        ILookup<Guid?, ProductCategory> children = snapshot.Categories.ToLookup(c => c.ParentId);

        return new PublicPriceListDto(
            priceList?.Title?.Translations,
            priceList?.Currency ?? DefaultCurrency,
            Sections(null));

        List<PriceListSectionDto> Sections(Guid? parentId) =>
        [
            .. children[parentId]
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Code, StringComparer.Ordinal)
                .Select(category => new PriceListSectionDto(
                    category.Code,
                    category.Name.Translations,
                    category.Description?.Translations,
                    [
                        .. items[category.Id]
                            .OrderBy(x => x.Product.SortOrder)
                            .ThenBy(x => x.Product.Code, StringComparer.Ordinal)
                            .Select(x => new PriceListItemDto(
                                x.Product.Code,
                                x.Product.Name.Translations,
                                x.Product.Description?.Translations,
                                x.Product.Badge?.Translations,
                                x.Price.UnitPrice)),
                    ],
                    Sections(category.Id)))
                .Where(section => section.Items.Count > 0 || section.Subsections.Count > 0 || section.Description is not null),
        ];
    }

    /// <summary>Backoffice: všechno, i skryté produkty a ceny do budoucna.</summary>
    public static CatalogAdminDto Admin(CatalogSnapshot snapshot, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        PriceList? priceList = snapshot.PriceList;
        Dictionary<Guid, ProductCategory> categories = snapshot.Categories.ToDictionary(c => c.Id);

        return new CatalogAdminDto(
            today,
            priceList?.Currency ?? DefaultCurrency,
            priceList?.Title?.Translations,
            [
                .. snapshot.Categories
                    .OrderBy(c => SortKey(c, categories), SortKeyComparer.Instance)
                    .Select(c => new AdminCategoryDto(
                        c.Id, c.Code, c.ParentId, c.Name.Translations, c.Description?.Translations, c.SortOrder)),
            ],
            [
                .. snapshot.Products
                    .OrderBy(p => categories.TryGetValue(p.CategoryId, out ProductCategory? c) ? SortKey(c, categories) : [], SortKeyComparer.Instance)
                    .ThenBy(p => p.SortOrder)
                    .ThenBy(p => p.Code, StringComparer.Ordinal)
                    .Select(p => new AdminProductDto(
                        p.Id,
                        p.Code,
                        p.CategoryId,
                        p.Name.Translations,
                        p.Description?.Translations,
                        p.Badge?.Translations,
                        p.ImagePath,
                        p.SortOrder,
                        new ProductVisibilityDto(
                            p.Channels.HasFlag(ProductChannels.PriceList),
                            p.Channels.HasFlag(ProductChannels.Eshop),
                            p.Channels.HasFlag(ProductChannels.Pos)),
                        p.IsActive,
                        priceList?.PriceOn(p.Id, today) is { } current ? ToDto(current) : null,
                        priceList is null ? [] : [.. priceList.ScheduledPrices(p.Id, today).Select(ToDto)])),
            ]);
    }

    private static PriceDto ToDto(PriceItem item) => new(item.UnitPrice, item.VatRate, item.ValidFrom);

    private static readonly IReadOnlyDictionary<string, string> EmptyText = new Dictionary<string, string>();

    /// <summary>Pořadí kategorie ve stromu: pořadí předků od kořene, pak její vlastní.</summary>
    private static List<(int Order, string Code)> SortKey(ProductCategory category, Dictionary<Guid, ProductCategory> all)
    {
        List<(int, string)> key = [];
        for (ProductCategory? current = category; current is not null;
             current = current.ParentId is { } parentId && all.TryGetValue(parentId, out ProductCategory? parent) ? parent : null)
        {
            key.Insert(0, (current.SortOrder, current.Code));
        }

        return key;
    }

    /// <summary>Lexikografické srovnání cest ve stromu — rodič před svými dětmi.</summary>
    private sealed class SortKeyComparer : IComparer<List<(int Order, string Code)>>
    {
        public static readonly SortKeyComparer Instance = new();

        public int Compare(List<(int Order, string Code)>? x, List<(int Order, string Code)>? y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            for (int i = 0; i < Math.Min(x.Count, y.Count); i++)
            {
                int byOrder = x[i].Order.CompareTo(y[i].Order);
                if (byOrder != 0)
                {
                    return byOrder;
                }

                int byCode = string.CompareOrdinal(x[i].Code, y[i].Code);
                if (byCode != 0)
                {
                    return byCode;
                }
            }

            return x.Count.CompareTo(y.Count);
        }
    }
}
