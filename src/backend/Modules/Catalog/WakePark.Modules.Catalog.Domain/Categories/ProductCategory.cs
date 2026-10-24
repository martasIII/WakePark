using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Domain.Categories;

/// <summary>
/// Skupina produktů („Jízdné“, „Půjčovna“, „Vouchery“) — sekce ceníku na webu i skupina
/// v e-shopu. Slouží jen k zobrazení, chování produktu na kategorii nezávisí.
/// </summary>
/// <remarks>
/// Kategorie tvoří strom (<see cref="ParentId"/>), hloubku hlídá <see cref="CategoryTree"/>.
/// Popis je poznámka sekce ceníku („Po dobu jízdného.“); kategorie s popisem se v ceníku
/// ukáže i bez produktů („Firemní akce — napište nám“).
/// </remarks>
public sealed class ProductCategory : AggregateRoot, IAuditable, ITenantOwned
{
    private ProductCategory()
    {
    }

    private ProductCategory(Guid id, Guid enterpriseId, string code)
        : base(id)
    {
        EnterpriseId = enterpriseId;
        Code = code;
    }

    public Guid EnterpriseId { get; private set; }

    /// <summary>Stálý kód, na webu i kotva sekce (<c>/cenik#vouchery</c>).</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Nadřazená kategorie; null = kategorie nejvyšší úrovně.</summary>
    public Guid? ParentId { get; private set; }

    public LocalizedText Name { get; private set; } = null!;

    public LocalizedText? Description { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>Nová kategorie nejvyšší úrovně. Zařazení pod jinou: <see cref="CategoryTree.Move"/>.</summary>
    public static ProductCategory Create(
        Guid enterpriseId,
        string code,
        LocalizedText name,
        LocalizedText? description,
        int sortOrder)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("category.enterprise_required", "Kategorie musí patřit provozovateli.");
        }

        ProductCategory category = new(NewId(), enterpriseId, CatalogCode.Normalize(code));
        category.Update(name, description, sortOrder);
        return category;
    }

    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool Update(LocalizedText name, LocalizedText? description, int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (Name == name && Description == description && SortOrder == sortOrder)
        {
            return false;
        }

        Name = name;
        Description = description;
        SortOrder = sortOrder;
        return true;
    }

    /// <summary>Změna rodiče bez kontroly stromu — volá ji jen <see cref="CategoryTree"/>.</summary>
    internal bool SetParent(Guid? parentId)
    {
        if (ParentId == parentId)
        {
            return false;
        }

        ParentId = parentId;
        return true;
    }
}
