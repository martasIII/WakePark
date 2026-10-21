using WakePark.BuildingBlocks.Domain;

namespace WakePark.Modules.Catalog.Domain.Pricing;

/// <summary>Cena jednoho produktu v ceníku. Mění se jen přes <see cref="PriceList"/>.</summary>
public sealed class PriceItem : Entity
{
    private PriceItem()
    {
    }

    internal PriceItem(Guid priceListId, Guid productId, decimal unitPrice, decimal vatRate)
        : base(NewId())
    {
        PriceListId = priceListId;
        ProductId = productId;
        UnitPrice = unitPrice;
        VatRate = vatRate;
    }

    public Guid PriceListId { get; private set; }

    public Guid ProductId { get; private set; }

    /// <summary>Cena včetně DPH — tak ji zákazník vidí a tak ji vlastník zadává.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Sazba DPH v procentech.</summary>
    public decimal VatRate { get; private set; }

    internal bool Change(decimal unitPrice, decimal vatRate)
    {
        if (UnitPrice == unitPrice && VatRate == vatRate)
        {
            return false;
        }

        UnitPrice = unitPrice;
        VatRate = vatRate;
        return true;
    }
}
