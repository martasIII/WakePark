using System.Globalization;

using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.Modules.Billing.Domain.Orders;

namespace WakePark.Modules.Billing.Domain.Invoices;

/// <summary>
/// Daňový doklad k zaplacené objednávce. Vystavuje se při spárování platby, ve stejné
/// transakci jako zaplacení objednávky — zaplacená objednávka bez dokladu nevznikne.
/// </summary>
/// <remarks>
/// Finanční doklad je **neměnný**: prodávající, kupující i řádky se zamrazí k okamžiku
/// vystavení a nikdy se neupravují. Oprava = nový doklad (dobropis), ne UPDATE.
/// Jednoúčelový poukaz se sazbou 12 % (ADR 0006): DUZP = den připsání platby.
/// </remarks>
public sealed class Invoice : AggregateRoot, ITenantOwned, IAuditable
{
    private readonly List<InvoiceLine> _lines = [];

    private Invoice()
    {
    }

    private Invoice(Guid id)
        : base(id)
    {
    }

    public Guid EnterpriseId { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>
    /// Číslo dokladu, např. <c>FV_E_2026-000001</c>. <c>E</c> = e-shop: odliší řadu od faktur, které
    /// Wake Park vystavuje v iDokladu. Řada je bez mezer.
    /// </summary>
    public string Number { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public long Sequence { get; private set; }

    public DateOnly IssuedOn { get; private set; }

    /// <summary>Datum uskutečnění zdanitelného plnění — u jednoúčelového poukazu den přijetí platby.</summary>
    public DateOnly TaxableSupplyOn { get; private set; }

    public string VariableSymbol { get; private set; } = string.Empty;

    public string Currency { get; private set; } = string.Empty;

    public InvoiceParty Seller { get; private set; } = null!;

    public InvoiceParty Buyer { get; private set; } = null!;

    public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();

    public decimal TotalNet { get; private set; }

    public decimal TotalVat { get; private set; }

    public decimal TotalGross { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static string FormatNumber(int year, long sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"FV_E_{year}-{sequence:000000}");

    public static Invoice Issue(
        Order order,
        InvoiceParty seller,
        int year,
        long sequence,
        DateOnly issuedOn,
        DateOnly taxableSupplyOn)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(seller);

        if (order.Status != OrderStatus.Paid)
        {
            throw new DomainException("invoice.order_not_paid", $"Doklad jde vystavit jen k zaplacené objednávce, {order.Number} zaplacená není.");
        }

        if (order.Lines.Count == 0)
        {
            throw new DomainException("invoice.no_lines", "Objednávka bez řádků — doklad by byl prázdný.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);

        Invoice invoice = new(NewId())
        {
            EnterpriseId = order.EnterpriseId,
            OrderId = order.Id,
            Number = FormatNumber(year, sequence),
            Year = year,
            Sequence = sequence,
            IssuedOn = issuedOn,
            TaxableSupplyOn = taxableSupplyOn,
            VariableSymbol = order.Number.ToString(CultureInfo.InvariantCulture),
            Currency = order.Currency,
            Seller = seller,
            Buyer = InvoiceParty.Buyer(order.Customer),
        };

        // Doklad je vždy česky — proto Description (česky), ne DisplayName v jazyce zákazníka.
        foreach (OrderLine line in order.Lines)
        {
            invoice._lines.Add(new InvoiceLine(
                line.Description, line.Quantity, line.UnitPrice, line.VatRate, line.LineNet, line.LineVat, line.LineGross));
        }

        invoice.TotalNet = invoice._lines.Sum(l => l.Net);
        invoice.TotalVat = invoice._lines.Sum(l => l.Vat);
        invoice.TotalGross = invoice._lines.Sum(l => l.Gross);
        return invoice;
    }
}

/// <summary>Řádek dokladu — kopie řádku objednávky, ceny včetně DPH.</summary>
public sealed record InvoiceLine(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal Net,
    decimal Vat,
    decimal Gross);

/// <summary>Prodávající nebo kupující, zamrazený v dokladu.</summary>
public sealed class InvoiceParty
{
    private InvoiceParty()
    {
    }

    public InvoiceParty(string name, string? companyId, string? vatId, string? address, string? registration = null, string? contactName = null, string? email = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        CompanyId = companyId;
        VatId = vatId;
        Address = address;
        Registration = registration;
        ContactName = contactName;
        Email = email;
    }

    /// <summary>Firma, nebo jméno a příjmení u fyzické osoby.</summary>
    public string Name { get; private set; } = string.Empty;

    public string? CompanyId { get; private set; }

    public string? VatId { get; private set; }

    public string? Address { get; private set; }

    /// <summary>Zápis v obchodním rejstříku (u prodávajícího povinný údaj).</summary>
    public string? Registration { get; private set; }

    /// <summary>U firmy jméno objednatele.</summary>
    public string? ContactName { get; private set; }

    public string? Email { get; private set; }

    public static InvoiceParty Buyer(Customers.CustomerContact customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return customer.CompanyName is { } company
            ? new InvoiceParty(company, customer.CompanyId, customer.VatId, customer.CompanyAddress, null, customer.Name, customer.Email)
            : new InvoiceParty(customer.Name, null, null, null, null, null, customer.Email);
    }
}
