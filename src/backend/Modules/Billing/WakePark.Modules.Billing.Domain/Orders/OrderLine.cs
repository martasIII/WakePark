using WakePark.BuildingBlocks.Domain;

namespace WakePark.Modules.Billing.Domain.Orders;

/// <summary>
/// Řádek objednávky. Název, cena i sazba DPH jsou **zamrazené** k okamžiku objednání —
/// pozdější změna katalogu se objednávky ani dokladu netkne. Na produkt odkazuje jen Id.
/// </summary>
public sealed class OrderLine : Entity
{
    public const int DescriptionMaxLength = 500;

    private OrderLine()
    {
    }

    internal OrderLine(Guid orderId, OrderLineDraft draft)
        : base(NewId())
    {
        OrderId = orderId;
        ProductId = draft.ProductId;
        ProductCode = draft.ProductCode;
        Description = draft.Description;
        DisplayName = draft.DisplayName;
        Quantity = draft.Quantity;
        UnitPrice = draft.UnitPrice;
        VatRate = draft.VatRate;

        LineGross = UnitPrice * Quantity;
        LineVat = VatFromGross(LineGross, VatRate);
        LineNet = LineGross - LineVat;
    }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    /// <summary>Název česky — pro daňový doklad, ten je vždy v češtině.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Název v jazyce zákazníka — pro stránku objednávky a e-mail.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    /// <summary>Cena za kus včetně DPH.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Sazba DPH v procentech.</summary>
    public decimal VatRate { get; private set; }

    public decimal LineGross { get; private set; }

    public decimal LineVat { get; private set; }

    public decimal LineNet { get; private set; }

    /// <summary>
    /// DPH z ceny včetně daně (výpočet „shora“): daň = cena × sazba / (100 + sazba),
    /// zaokrouhleno na haléře. Základ je rozdíl, aby základ + daň dal vždy přesně cenu.
    /// </summary>
    public static decimal VatFromGross(decimal gross, decimal vatRate) =>
        decimal.Round(gross * vatRate / (100m + vatRate), 2, MidpointRounding.AwayFromZero);
}

/// <summary>Podklad pro řádek — údaje z katalogu, které se do objednávky zamrazí.</summary>
public sealed record OrderLineDraft(
    Guid ProductId,
    string ProductCode,
    string Description,
    string DisplayName,
    int Quantity,
    decimal UnitPrice,
    decimal VatRate);
