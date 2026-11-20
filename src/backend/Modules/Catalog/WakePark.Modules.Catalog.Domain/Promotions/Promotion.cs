using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Domain.Promotions;

/// <summary>
/// Akční sleva v e-shopu: výchozí procento z ceny všech produktů kategorie (i podkategorií)
/// po celé dny od–do včetně, v čase areálu (přelom o půlnoci). Jednotlivé produkty kategorie
/// můžou mít vlastní procento, nebo být z akce vyřazené (0 %).
/// </summary>
/// <remarks>
/// Jen e-shop — ceník na webu ani pokladna akci neznají. Objednávka si cenu zamrazí, takže
/// změna nebo smazání akce prodané neovlivní. Dvě akce na stejnou kategorii se nesmí časově
/// překrývat; to hlídá aplikační vrstva, která vidí všechny akce provozovatele.
/// </remarks>
public sealed class Promotion : AggregateRoot, IAuditable, ITenantOwned
{
    public const int MinDiscountPercent = 1;
    public const int MaxDiscountPercent = 90;
    public const int MaxNameLength = 60;

    private List<ProductDiscount> _productDiscounts = [];

    private Promotion()
    {
    }

    private Promotion(Guid id, Guid enterpriseId)
        : base(id)
    {
        EnterpriseId = enterpriseId;
    }

    public Guid EnterpriseId { get; private set; }

    /// <summary>Název akce, ukáže se v e-shopu („Vánoční akce“).</summary>
    public LocalizedText Name { get; private set; } = null!;

    public Guid CategoryId { get; private set; }

    /// <summary>Výchozí sleva v celých procentech — platí pro produkty bez vlastního procenta.</summary>
    public int DiscountPercent { get; private set; }

    /// <summary>Produkty s jiným procentem než výchozím (0 = bez slevy).</summary>
    public IReadOnlyList<ProductDiscount> ProductDiscounts => _productDiscounts;

    /// <summary>První den akce (od půlnoci, čas areálu).</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>Poslední den akce včetně (do půlnoci).</summary>
    public DateOnly ValidTo { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Nová akce. <paramref name="today"/> je dnešní datum v čase areálu — akce, která by už
    /// skončila, nedává smysl.
    /// </summary>
    public static Promotion Create(
        Guid enterpriseId,
        LocalizedText name,
        Guid categoryId,
        int discountPercent,
        IEnumerable<ProductDiscount> productDiscounts,
        DateOnly validFrom,
        DateOnly validTo,
        DateOnly today)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("promotion.enterprise_required", "Akce musí patřit provozovateli.");
        }

        Promotion promotion = new(NewId(), enterpriseId);
        promotion.Change(name, categoryId, discountPercent, productDiscounts, validFrom, validTo, today);
        return promotion;
    }

    /// <summary>
    /// Úprava akce. Že produkty s vlastním procentem patří do kategorie akce, hlídá aplikační
    /// vrstva; výjimka stejná jako výchozí procento se neukládá.
    /// </summary>
    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool Change(
        LocalizedText name,
        Guid categoryId,
        int discountPercent,
        IEnumerable<ProductDiscount> productDiscounts,
        DateOnly validFrom,
        DateOnly validTo,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(productDiscounts);

        if (name.Translations.Values.Any(text => text.Length > MaxNameLength))
        {
            throw new DomainException("promotion.name_too_long", $"Název akce může mít nejvýš {MaxNameLength} znaků.");
        }

        if (categoryId == Guid.Empty)
        {
            throw new DomainException("promotion.category_required", "Vyberte kategorii, na kterou akce platí.");
        }

        if (discountPercent is < MinDiscountPercent or > MaxDiscountPercent)
        {
            throw new DomainException(
                "promotion.invalid_discount",
                $"Sleva musí být {MinDiscountPercent} až {MaxDiscountPercent} %.");
        }

        List<ProductDiscount> discounts = [.. productDiscounts];
        if (discounts.Any(d => d.DiscountPercent is < 0 or > MaxDiscountPercent))
        {
            throw new DomainException(
                "promotion.invalid_product_discount",
                $"Sleva u produktu musí být 0 až {MaxDiscountPercent} % (0 = bez slevy).");
        }

        if (discounts.Any(d => d.ProductId == Guid.Empty) || discounts.DistinctBy(d => d.ProductId).Count() != discounts.Count)
        {
            throw new DomainException("promotion.duplicate_product", "Každý produkt může mít v akci jen jedno procento.");
        }

        discounts = [.. discounts.Where(d => d.DiscountPercent != discountPercent).OrderBy(d => d.ProductId)];

        if (validTo < validFrom)
        {
            throw new DomainException("promotion.invalid_range", "Akce musí skončit nejdřív v den, kdy začíná.");
        }

        if (validTo < today)
        {
            throw new DomainException("promotion.already_ended", "Akce by už skončila — konec musí být dnes nebo později.");
        }

        bool changed = Name != name || CategoryId != categoryId || DiscountPercent != discountPercent
            || ValidFrom != validFrom || ValidTo != validTo || !_productDiscounts.SequenceEqual(discounts);

        Name = name;
        CategoryId = categoryId;
        DiscountPercent = discountPercent;
        _productDiscounts = discounts;
        ValidFrom = validFrom;
        ValidTo = validTo;
        return changed;
    }

    public bool IsActiveOn(DateOnly date) => ValidFrom <= date && date <= ValidTo;

    /// <summary>Stejná kategorie a aspoň jeden společný den.</summary>
    public bool Overlaps(Promotion other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.Id != Id && other.CategoryId == CategoryId && other.ValidFrom <= ValidTo && ValidFrom <= other.ValidTo;
    }

    /// <summary>Procento pro produkt: vlastní, jinak výchozí. 0 = produkt v akci není.</summary>
    public int DiscountFor(Guid productId) =>
        _productDiscounts.Find(d => d.ProductId == productId)?.DiscountPercent ?? DiscountPercent;

    /// <summary>
    /// Cena produktu po slevě, zaokrouhlená na celé koruny nahoru (1 490 Kč − 15 % = 1 267 Kč).
    /// </summary>
    public decimal Apply(Guid productId, decimal unitPrice) => Math.Ceiling(unitPrice * (100 - DiscountFor(productId)) / 100m);
}
