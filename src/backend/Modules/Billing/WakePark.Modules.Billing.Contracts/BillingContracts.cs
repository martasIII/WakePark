namespace WakePark.Modules.Billing.Contracts;

/// <summary>Objednávka z webu. Ceny se neposílají — server je bere z katalogu.</summary>
public sealed record PlaceOrderRequest(
    string Language,
    PlaceOrderCustomer Customer,
    IReadOnlyList<PlaceOrderLine> Lines,
    bool TermsAccepted);

public sealed record PlaceOrderCustomer(
    string Name,
    string Email,
    string? Phone,
    string? CompanyName,
    string? CompanyId,
    string? VatId);

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
    decimal LineGross);

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
