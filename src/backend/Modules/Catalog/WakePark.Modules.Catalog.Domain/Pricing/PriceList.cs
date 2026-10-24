using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Domain.Pricing;

/// <summary>
/// Ceník provozovatele. Každý produkt v něm má řadu cen s platností od data — změna platí
/// hned (od dneška), nebo od zvoleného dne (ceny na příští sezónu se připraví předem, ADR 0009).
/// </summary>
/// <remarks>
/// Historie cen se nemaže: změna = nová cena od data, ne přepis. Přepsat jde jen cena, která
/// platí od dneška nebo teprve začne platit. Objednávka si cenu zamrazí, takže změna ceníku
/// prodané neovlivní. Segmenty (junior, víkend) zatím nejsou.
/// </remarks>
public sealed class PriceList : AggregateRoot, IAuditable, ITenantOwned
{
    public const decimal MaxVatRate = 100m;

    private readonly List<PriceItem> _items = [];

    private PriceList()
    {
    }

    private PriceList(Guid id, Guid enterpriseId, string name, string currency, DateOnly validFrom)
        : base(id)
    {
        EnterpriseId = enterpriseId;
        Name = name;
        Currency = currency;
        ValidFrom = validFrom;
        IsActive = true;
    }

    public Guid EnterpriseId { get; private set; }

    /// <summary>Interní název ceníku.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Podtitulek ceníku na webu, např. „Sezóna 2027“.</summary>
    public LocalizedText? Title { get; private set; }

    /// <summary>Jedna měna na celý ceník. Sčítat ceny v různých měnách nedává smysl.</summary>
    public string Currency { get; private set; } = string.Empty;

    public DateOnly ValidFrom { get; private set; }

    public DateOnly? ValidTo { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<PriceItem> Items => _items.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static PriceList Create(Guid enterpriseId, string name, string currency, DateOnly validFrom)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("price_list.enterprise_required", "Ceník musí patřit provozovateli.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        string code = currency.Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException(
                "price_list.invalid_currency",
                $"Měna musí být třípísmenný kód podle ISO 4217, ne '{currency}'.");
        }

        return new PriceList(NewId(), enterpriseId, name.Trim(), code, validFrom);
    }

    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool SetTitle(LocalizedText? title)
    {
        if (Title == title)
        {
            return false;
        }

        Title = title;
        return true;
    }

    /// <summary>Cena, která v daný den platí. Null = produkt v ten den cenu nemá.</summary>
    public PriceItem? PriceOn(Guid productId, DateOnly date) =>
        _items
            .Where(item => item.ProductId == productId && item.ValidFrom <= date)
            .MaxBy(item => item.ValidFrom);

    /// <summary>Ceny, které teprve začnou platit, seřazené od nejbližší.</summary>
    public IReadOnlyList<PriceItem> ScheduledPrices(Guid productId, DateOnly today) =>
        [.. _items.Where(item => item.ProductId == productId && item.ValidFrom > today).OrderBy(item => item.ValidFrom)];

    /// <summary>
    /// Nastaví cenu produktu od data. Cena od dneška, nebo od budoucího dne; do minulosti ne —
    /// za minulé dny se už mohlo prodávat. Nula je dovolená (helma zdarma v ceníku); jestli
    /// produkt s nulou smí do e-shopu, hlídá aplikační vrstva. <paramref name="today"/> je dnešní
    /// datum v čase areálu.
    /// </summary>
    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool SetPrice(Guid productId, decimal unitPrice, decimal vatRate, DateOnly validFrom, DateOnly today)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("price_list.product_required", "Cena musí patřit produktu.");
        }

        if (unitPrice < 0m)
        {
            throw new DomainException("price_list.invalid_price", "Cena nesmí být záporná.");
        }

        if (vatRate is < 0m or > MaxVatRate)
        {
            throw new DomainException("price_list.invalid_vat_rate", "Sazba DPH musí být mezi 0 a 100 %.");
        }

        if (validFrom < today)
        {
            throw new DomainException("price_list.valid_from_in_past", "Cena může platit od dneška nebo od budoucího dne, ne zpětně.");
        }

        decimal price = decimal.Round(unitPrice, 2, MidpointRounding.ToEven);

        PriceItem? sameDay = _items.Find(item => item.ProductId == productId && item.ValidFrom == validFrom);
        if (sameDay is not null)
        {
            return sameDay.Change(price, vatRate);
        }

        // Stejná cena, jaká v ten den už platí, by do historie přidala jen šum.
        PriceItem? effective = PriceOn(productId, validFrom);
        if (effective is not null && effective.UnitPrice == price && effective.VatRate == vatRate)
        {
            return false;
        }

        _items.Add(new PriceItem(Id, productId, price, vatRate, validFrom));
        return true;
    }

    /// <summary>Zruší cenu, která teprve začne platit. Platnou ani minulou cenu zrušit nejde.</summary>
    /// <returns><c>true</c>, pokud taková cena byla.</returns>
    public bool RemoveScheduledPrice(Guid productId, DateOnly validFrom, DateOnly today)
    {
        if (validFrom <= today)
        {
            throw new DomainException("price_list.price_already_valid", "Cena už platí — změňte ji novou cenou, zrušit ji nejde.");
        }

        return _items.RemoveAll(item => item.ProductId == productId && item.ValidFrom == validFrom) > 0;
    }
}
