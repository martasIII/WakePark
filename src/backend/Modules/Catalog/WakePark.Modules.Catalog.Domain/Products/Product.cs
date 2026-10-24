using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Domain.Products;

/// <summary>
/// Co se prodává — na webu v ceníku, v e-shopu i na pokladně (<see cref="Channels"/>).
/// Co produkt znamená na místě (sloty, počet jízd permanentky), přijde s pokladnou.
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

    /// <summary>Nepovinný — položka ceníku jako „Helma“ popis nepotřebuje.</summary>
    public LocalizedText? Description { get; private set; }

    /// <summary>Štítek v ceníku, např. „Nejvýhodnější“.</summary>
    public LocalizedText? Badge { get; private set; }

    /// <summary>Cesta k obrázku na veřejném webu, např. <c>/img/katalog/voucher-1.svg</c>.</summary>
    public string? ImagePath { get; private set; }

    public int SortOrder { get; private set; }

    public ProductChannels Channels { get; private set; }

    /// <summary>Skrytý produkt se nikde nenabízí, ale dál existuje kvůli prodaným poukazům.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Product Create(Guid enterpriseId, string code, ProductDetails details)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("product.enterprise_required", "Produkt musí patřit provozovateli.");
        }

        Product product = new(NewId(), enterpriseId, CatalogCode.Normalize(code));
        product.Update(details);
        return product;
    }

    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool Update(ProductDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(details.Name);

        if (details.CategoryId == Guid.Empty)
        {
            throw new DomainException("product.category_required", "Produkt musí mít kategorii.");
        }

        if ((details.Channels & ~ProductChannels.All) != 0)
        {
            throw new DomainException("product.invalid_channels", "Neznámé místo zobrazení produktu.");
        }

        string? image = NormalizeImagePath(details.ImagePath);

        if (CategoryId == details.CategoryId
            && Name == details.Name
            && Description == details.Description
            && Badge == details.Badge
            && ImagePath == image
            && SortOrder == details.SortOrder
            && Channels == details.Channels)
        {
            return false;
        }

        CategoryId = details.CategoryId;
        Name = details.Name;
        Description = details.Description;
        Badge = details.Badge;
        ImagePath = image;
        SortOrder = details.SortOrder;
        Channels = details.Channels;
        return true;
    }

    /// <summary>Aktivní a zobrazovaný v daném místě.</summary>
    public bool IsOfferedIn(ProductChannels channel) => IsActive && Channels.HasFlag(channel);

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

/// <summary>Vše, co se na produktu dá upravit. Kód ne — ten je stálý.</summary>
public sealed record ProductDetails(
    Guid CategoryId,
    LocalizedText Name,
    LocalizedText? Description,
    LocalizedText? Badge,
    string? ImagePath,
    int SortOrder,
    ProductChannels Channels);
