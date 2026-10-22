namespace WakePark.Modules.Billing.Contracts;

/// <summary>Objednávka z webu. Ceny se neposílají — server je bere z katalogu.</summary>
public sealed record PlaceOrderRequest(
    string Language,
    PlaceOrderCustomer Customer,
    IReadOnlyList<PlaceOrderLine> Lines,
    bool TermsAccepted);

/// <summary>
/// Kupující. U nákupu na firmu jen IČO — název, DIČ a sídlo doplní server z registru ARES,
/// aby se na doklad nedostal překlep ani vymyšlená firma.
/// </summary>
public sealed record PlaceOrderCustomer(
    string Name,
    string Email,
    string? Phone,
    string? CompanyId);

/// <summary>Firma z registru ARES. DIČ jen u plátce DPH.</summary>
public sealed record CompanyDto(string CompanyId, string Name, string? VatId, string? Address);

public sealed record PlaceOrderLine(Guid ProductId, int Quantity);

/// <summary>Založená objednávka. Token je klíč do adresy stránky objednávky.</summary>
public sealed record PlacedOrderDto(long Number, string AccessToken);

public sealed record OrderDto(
    long Number,
    string Status,
    string Language,
    string CustomerName,
    string CustomerEmail,
    string? CompanyName,
    IReadOnlyList<OrderLineDto> Lines,
    decimal TotalGross,
    decimal TotalVat,
    decimal TotalNet,
    string Currency,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset PaymentDueAtUtc,
    PaymentInstructionsDto? Payment);

public sealed record OrderLineDto(
    string ProductCode,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal LineGross,
    Guid ProductId);

/// <summary>Daňový doklad — pro PDF v e-mailu a později pro backoffice.</summary>
public sealed record InvoiceDto(
    string Number,
    DateOnly IssuedOn,
    DateOnly TaxableSupplyOn,
    string VariableSymbol,
    string Currency,
    InvoicePartyDto Seller,
    InvoicePartyDto Buyer,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<InvoiceVatDto> VatRecap,
    decimal TotalNet,
    decimal TotalVat,
    decimal TotalGross);

public sealed record InvoicePartyDto(
    string Name,
    string? CompanyId,
    string? VatId,
    string? Address,
    string? Registration,
    string? ContactName,
    string? Email);

public sealed record InvoiceLineDto(string Description, int Quantity, decimal UnitPrice, decimal VatRate, decimal Net, decimal Vat, decimal Gross);

/// <summary>Rekapitulace DPH podle sazeb.</summary>
public sealed record InvoiceVatDto(decimal VatRate, decimal Net, decimal Vat, decimal Gross);

/// <summary>
/// Jak zaplatit. Jen u objednávky, která na platbu čeká. <see cref="Spayd"/> je obsah
/// QR kódu pro platbu (formát Short Payment Descriptor, ČBA).
/// </summary>
public sealed record PaymentInstructionsDto(
    string AccountNumber,
    string Iban,
    string? Bic,
    string VariableSymbol,
    decimal Amount,
    string Currency,
    string Message,
    string Spayd);
