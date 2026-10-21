using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Domain.Categories;

/// <summary>
/// Skupina produktů na webu („Vouchery“, „Permanentky“). Slouží jen k zobrazení —
/// chování produktu na kategorii nezávisí.
/// </summary>
public sealed class ProductCategory : AggregateRoot, IAuditable, ITenantOwned
{
    private ProductCategory()
    {
    }

    private ProductCategory(Guid id, Guid enterpriseId, string code, LocalizedText name, int sortOrder)
        : base(id)
    {
        EnterpriseId = enterpriseId;
        Code = code;
        Name = name;
        SortOrder = sortOrder;
    }

    public Guid EnterpriseId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public LocalizedText Name { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static ProductCategory Create(Guid enterpriseId, string code, LocalizedText name, int sortOrder)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("category.enterprise_required", "Kategorie musí patřit provozovateli.");
        }

        ArgumentNullException.ThrowIfNull(name);

        return new ProductCategory(NewId(), enterpriseId, CatalogCode.Normalize(code), name, sortOrder);
    }

    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool Update(LocalizedText name, int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (Name == name && SortOrder == sortOrder)
        {
            return false;
        }

        Name = name;
        SortOrder = sortOrder;
        return true;
    }
}
