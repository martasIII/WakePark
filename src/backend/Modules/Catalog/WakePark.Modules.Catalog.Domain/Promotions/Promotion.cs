using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Domain.Promotions;

/// <summary>
/// Akční sleva v e-shopu: procento z ceny všech produktů kategorie (i podkategorií) po celé
/// dny od–do včetně, v čase areálu (přelom o půlnoci).
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

    /// <summary>Sleva v celých procentech.</summary>
    public int DiscountPercent { get; private set; }

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
        DateOnly validFrom,
        DateOnly validTo,
        DateOnly today)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("promotion.enterprise_required", "Akce musí patřit provozovateli.");
        }

        Promotion promotion = new(NewId(), enterpriseId);
        promotion.Change(name, categoryId, discountPercent, validFrom, validTo, today);
        return promotion;
    }

    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool Change(LocalizedText name, Guid categoryId, int discountPercent, DateOnly validFrom, DateOnly validTo, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(name);

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

        if (validTo < validFrom)
        {
            throw new DomainException("promotion.invalid_range", "Akce musí skončit nejdřív v den, kdy začíná.");
        }

        if (validTo < today)
        {
            throw new DomainException("promotion.already_ended", "Akce by už skončila — konec musí být dnes nebo později.");
        }

        bool changed = Name != name || CategoryId != categoryId || DiscountPercent != discountPercent
            || ValidFrom != validFrom || ValidTo != validTo;

        Name = name;
        CategoryId = categoryId;
        DiscountPercent = discountPercent;
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

    /// <summary>Cena po slevě, zaokrouhlená na celé koruny nahoru (1 490 Kč − 15 % = 1 267 Kč).</summary>
    public decimal Apply(decimal unitPrice) => Math.Ceiling(unitPrice * (100 - DiscountPercent) / 100m);
}
