using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.Modules.Billing.Domain.Customers;

namespace WakePark.Modules.Billing.Domain.Orders;

/// <summary>
/// Objednávka z e-shopu. Vzniká rovnou jako „čeká na platbu“ — platí se převodem,
/// variabilním symbolem je číslo objednávky (ADR 0006).
/// </summary>
public sealed class Order : AggregateRoot, ITenantOwned, IAuditable
{
    /// <summary>Strop na kusy jedné položky. Víc voucherů najednou je spíš překlep nebo robot.</summary>
    public const int MaxQuantityPerLine = 20;

    public const int MaxLines = 20;

    /// <summary>256 bitů náhody jako 64 malých hex znaků — adresa se nerozbije ani převodem na malá písmena.</summary>
    public const int AccessTokenLength = 64;

    private readonly List<OrderLine> _lines = [];

    private Order()
    {
    }

    private Order(
        Guid id,
        Guid enterpriseId,
        long number,
        string accessToken,
        CustomerContact customer,
        string language,
        string currency,
        DateTimeOffset placedAtUtc,
        DateTimeOffset paymentDueAtUtc)
        : base(id)
    {
        EnterpriseId = enterpriseId;
        Number = number;
        AccessToken = accessToken;
        Customer = customer;
        Language = language;
        Currency = currency;
        PlacedAtUtc = placedAtUtc;
        PaymentDueAtUtc = paymentDueAtUtc;
        TermsAcceptedAtUtc = placedAtUtc;
        Status = OrderStatus.AwaitingPayment;
    }

    public Guid EnterpriseId { get; private set; }

    /// <summary>Číslo objednávky, zároveň variabilní symbol platby (např. 261000).</summary>
    public long Number { get; private set; }

    /// <summary>
    /// Náhodný klíč do adresy stránky objednávky. Čísla jdou po sobě a dají se uhodnout,
    /// proto se stránka nikdy neotevírá jen podle čísla.
    /// </summary>
    public string AccessToken { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public CustomerContact Customer { get; private set; } = null!;

    /// <summary>Jazyk zákazníka — podle něj se píše e-mail a stránka objednávky.</summary>
    public string Language { get; private set; } = string.Empty;

    public string Currency { get; private set; } = string.Empty;

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    public decimal TotalGross { get; private set; }

    public decimal TotalVat { get; private set; }

    public decimal TotalNet { get; private set; }

    public DateTimeOffset PlacedAtUtc { get; private set; }

    public DateTimeOffset PaymentDueAtUtc { get; private set; }

    /// <summary>Kdy zákazník odsouhlasil obchodní podmínky — bez souhlasu objednávka nevznikne.</summary>
    public DateTimeOffset TermsAcceptedAtUtc { get; private set; }

    /// <summary>Kdy se platba spárovala. Datum zaúčtování v bance nese bankovní pohyb.</summary>
    public DateTimeOffset? PaidAtUtc { get; private set; }

    /// <summary>Bankovní pohyb, který objednávku zaplatil.</summary>
    public Guid? PaidByBankTransactionId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Platba dorazila. Přijme se i po splatnosti, dokonce i u zrušené objednávky — poukazů
    /// není omezený počet a přijaté peníze by se stejně musely vyfakturovat nebo vrátit (ADR 0006).
    /// </summary>
    public void MarkPaid(DateTimeOffset paidAtUtc, Guid bankTransactionId)
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("order.already_paid", $"Objednávka {Number} už je zaplacená.");
        }

        Status = OrderStatus.Paid;
        PaidAtUtc = paidAtUtc;
        PaidByBankTransactionId = bankTransactionId;
        Raise(new OrderPaid(Guid.CreateVersion7(), paidAtUtc, EnterpriseId, Id, AccessToken));
    }

    public static Order Place(
        Guid enterpriseId,
        long number,
        string accessToken,
        CustomerContact customer,
        string language,
        string currency,
        IReadOnlyList<OrderLineDraft> lines,
        DateTimeOffset placedAtUtc,
        TimeSpan paymentDue)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("order.enterprise_required", "Objednávka musí patřit provozovateli.");
        }

        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (number <= 0)
        {
            throw new DomainException("order.invalid_number", "Číslo objednávky musí být kladné.");
        }

        if (accessToken is not { Length: AccessTokenLength })
        {
            throw new DomainException("order.invalid_token", "Přístupový klíč objednávky nemá správnou délku.");
        }

        if (lines.Count == 0)
        {
            throw new DomainException("order.empty", "Objednávka musí obsahovat aspoň jednu položku.");
        }

        if (lines.Count > MaxLines)
        {
            throw new DomainException("order.too_many_lines", "Objednávka má příliš mnoho položek.");
        }

        if (lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
        {
            throw new DomainException("order.duplicate_product", "Každý produkt smí být v objednávce jen jednou.");
        }

        if (lines.Any(l => l.Quantity is < 1 or > MaxQuantityPerLine))
        {
            throw new DomainException(
                "order.invalid_quantity",
                $"Počet kusů jedné položky musí být 1 až {MaxQuantityPerLine}.");
        }

        if (lines.Any(l => l.UnitPrice <= 0m))
        {
            throw new DomainException("order.invalid_price", "Cena položky musí být kladná.");
        }

        if (paymentDue <= TimeSpan.Zero)
        {
            throw new DomainException("order.invalid_due", "Splatnost musí být v budoucnu.");
        }

        Order order = new(
            NewId(),
            enterpriseId,
            number,
            accessToken,
            customer,
            language.Trim().ToLowerInvariant(),
            currency.Trim().ToUpperInvariant(),
            placedAtUtc,
            placedAtUtc + paymentDue);

        foreach (OrderLineDraft draft in lines)
        {
            order._lines.Add(new OrderLine(order.Id, draft));
        }

        order.TotalGross = order._lines.Sum(l => l.LineGross);
        order.TotalVat = order._lines.Sum(l => l.LineVat);
        order.TotalNet = order._lines.Sum(l => l.LineNet);

        order.Raise(new OrderPlaced(Guid.CreateVersion7(), placedAtUtc, order.EnterpriseId, order.Id, order.AccessToken));
        return order;
    }
}

public enum OrderStatus
{
    AwaitingPayment,
    Paid,
    Cancelled,
}
