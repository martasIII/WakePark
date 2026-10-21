using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Catalog.Domain.Pricing;

/// <summary>
/// Ceník provozovatele. Zatím jediný a bez segmentů (junior, víkend …) — ty přijdou, až je
/// bude něco potřebovat. Objednávka si cenu zamrazí, takže změna ceníku prodané neovlivní.
/// </summary>
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

    public string Name { get; private set; } = string.Empty;

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

    public PriceItem? FindItem(Guid productId) => _items.Find(item => item.ProductId == productId);

    /// <summary>Nastaví cenu produktu, případně ji v ceníku založí.</summary>
    /// <returns><c>true</c>, pokud se něco změnilo.</returns>
    public bool SetPrice(Guid productId, decimal unitPrice, decimal vatRate)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("price_list.product_required", "Cena musí patřit produktu.");
        }

        if (unitPrice <= 0m)
        {
            throw new DomainException("price_list.invalid_price", "Cena musí být kladná.");
        }

        if (vatRate is < 0m or > MaxVatRate)
        {
            throw new DomainException("price_list.invalid_vat_rate", "Sazba DPH musí být mezi 0 a 100 %.");
        }

        decimal price = decimal.Round(unitPrice, 2, MidpointRounding.ToEven);
        PriceItem? existing = FindItem(productId);

        if (existing is null)
        {
            _items.Add(new PriceItem(Id, productId, price, vatRate));
            return true;
        }

        return existing.Change(price, vatRate);
    }
}
