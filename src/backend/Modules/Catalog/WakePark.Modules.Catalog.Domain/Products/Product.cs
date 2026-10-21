using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Domain.Products;

/// <summary>
/// Co se prodává. Pro e-shop jen popis pro zákazníka — co produkt znamená na místě
/// (sloty, počet jízd permanentky), se doplní s backoffice (ADR 0006, upřesnění z 21. 10. 2026).
/// Cena není tady, ale v ceníku.
/// </summary>
/// <remarks>
/// Produkt se nikdy nemaže, jen se skryje. Odkazují na něj prodané objednávky a poukazy.
/// </remarks>
public sealed class Product : AggregateRoot, IAuditable, ITenantOwned
{
    public const int ImagePathMaxLength = 300;

    private Product()
    {
    }

    private Product(Guid id, Guid enterpriseId, string code)
        : base(id)
    {
        EnterpriseId = enterpriseId;
        Code = code;
        IsActive = true;
    }

    public Guid EnterpriseId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public Guid CategoryId { get; private set; }

    public LocalizedText Name { get; private set; } = null!;

    public LocalizedText Description { get; private set; } = null!;

    /// <summary>Cesta k obrázku na veřejném webu, např. <c>/img/katalog/voucher-1.svg</c>.</summary>
    public string? ImagePath { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Skrytý produkt se na webu nenabízí, ale dál existuje kvůli prodaným poukazům.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Product Create(
        Guid enterpriseId,
        string code,
        Guid categoryId,
        LocalizedText name,
        LocalizedText description,
        string? imagePath,
        int sortOrder)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("product.enterprise_required", "Produkt musí patřit provozovateli.");
        }

        Product product = new(NewId(), enterpriseId, CatalogCode.Normalize(code));
        product.Update(categoryId, name, description, imagePath, sortOrder);
        return product;
    }

    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool Update(
        Guid categoryId,
        LocalizedText name,
        LocalizedText description,
        string? imagePath,
        int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        if (categoryId == Guid.Empty)
        {
            throw new DomainException("product.category_required", "Produkt musí mít kategorii.");
        }

        string? image = NormalizeImagePath(imagePath);

        if (CategoryId == categoryId
            && Name == name
            && Description == description
            && ImagePath == image
            && SortOrder == sortOrder)
        {
            return false;
        }

        CategoryId = categoryId;
        Name = name;
        Description = description;
        ImagePath = image;
        SortOrder = sortOrder;
        return true;
    }

    /// <returns><c>true</c>, pokud byl produkt dosud aktivní.</returns>
    public bool Deactivate()
    {
        if (!IsActive)
        {
            return false;
        }

        IsActive = false;
        return true;
    }

    /// <returns><c>true</c>, pokud byl produkt dosud skrytý.</returns>
    public bool Activate()
    {
        if (IsActive)
        {
            return false;
        }

        IsActive = true;
        return true;
    }

    private static string? NormalizeImagePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return null;
        }

        string path = imagePath.Trim();

        // Jen cesta na vlastním webu. Absolutní adresa by obešla CSP a „..“ nemá v odkazu co dělat.
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal)
            || path.Contains("..", StringComparison.Ordinal) || path.Length > ImagePathMaxLength)
        {
            throw new DomainException(
                "product.invalid_image",
                $"Obrázek musí být cesta na webu začínající lomítkem, ne '{imagePath}'.");
        }

        return path;
    }
}
