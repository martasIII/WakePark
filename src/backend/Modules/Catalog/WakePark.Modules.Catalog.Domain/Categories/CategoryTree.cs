using WakePark.BuildingBlocks.Domain;

namespace WakePark.Modules.Catalog.Domain.Categories;

/// <summary>
/// Pravidla stromu kategorií. Model je obecný (libovolný rodič), ale ceník i e-shop počítají
/// nejvýš s kategorií a podkategorií (ADR 0009) — víc úrovní je změna <see cref="MaxDepth"/>.
/// </summary>
public static class CategoryTree
{
    public const int MaxDepth = 2;

    /// <summary>
    /// Zařadí kategorii pod rodiče (null = nejvyšší úroveň). <paramref name="all"/> jsou všechny
    /// kategorie provozovatele, včetně přesouvané.
    /// </summary>
    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public static bool Move(ProductCategory category, Guid? parentId, IReadOnlyCollection<ProductCategory> all)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(all);

        if (parentId is null)
        {
            return category.SetParent(null);
        }

        Dictionary<Guid, ProductCategory> byId = all.ToDictionary(c => c.Id);

        if (!byId.TryGetValue(parentId.Value, out ProductCategory? parent) || parent.EnterpriseId != category.EnterpriseId)
        {
            throw new DomainException("category.parent_not_found", "Nadřazená kategorie neexistuje.");
        }

        // Rodič nesmí být sama kategorie ani nikdo pod ní — vznikl by cyklus.
        for (ProductCategory? ancestor = parent; ancestor is not null; ancestor = Parent(ancestor, byId))
        {
            if (ancestor.Id == category.Id)
            {
                throw new DomainException("category.cycle", "Kategorii nejde zařadit pod ni samotnou ani pod její podkategorii.");
            }
        }

        if (Depth(parent, byId) + Height(category, all) > MaxDepth)
        {
            throw new DomainException(
                "category.too_deep",
                $"Kategorie smí mít nejvýš {MaxDepth} úrovně (kategorie a podkategorie).");
        }

        return category.SetParent(parent.Id);
    }

    /// <summary>Úroveň kategorie: nejvyšší = 1.</summary>
    private static int Depth(ProductCategory category, Dictionary<Guid, ProductCategory> byId)
    {
        int depth = 1;
        for (ProductCategory? ancestor = Parent(category, byId); ancestor is not null; ancestor = Parent(ancestor, byId))
        {
            depth++;
        }

        return depth;
    }

    /// <summary>Kolik úrovní zabírá kategorie i se vším pod sebou: bez podkategorií = 1.</summary>
    private static int Height(ProductCategory category, IReadOnlyCollection<ProductCategory> all) =>
        1 + all.Where(c => c.ParentId == category.Id).Select(c => Height(c, all)).DefaultIfEmpty(0).Max();

    private static ProductCategory? Parent(ProductCategory category, Dictionary<Guid, ProductCategory> byId) =>
        category.ParentId is { } id && byId.TryGetValue(id, out ProductCategory? parent) ? parent : null;
}
