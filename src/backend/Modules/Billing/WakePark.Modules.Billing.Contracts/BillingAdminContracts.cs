namespace WakePark.Modules.Billing.Contracts;

// Přehled objednávek v backoffice — jen čtení (ADR 0012).

/// <summary>Stránka seznamu objednávek, nejnovější první.</summary>
public sealed record OrderListDto(IReadOnlyList<OrderListItemDto> Items, int Total, int Page, int PageSize);

/// <summary>
/// Řádek seznamu. <c>Status</c>: <c>AwaitingPayment</c>, <c>Paid</c>, <c>Cancelled</c>.
/// <c>Summary</c> je stručný obsah, např. „2× Voucher pro dva, 1× 10 × 20 minut“.
/// </summary>
public sealed record OrderListItemDto(
    long Number,
    string Status,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset? PaidAtUtc,
    string CustomerName,
    string CustomerEmail,
    string? CompanyName,
    string Summary,
    decimal TotalGross,
    string Currency);

/// <summary>Detail objednávky: zákazník, položky s věnováním, platba, doklad a vystavené poukazy.</summary>
public sealed record OrderAdminDetailDto(
    long Number,
    string Status,
    string Language,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset PaymentDueAtUtc,
    DateTimeOffset? PaymentReminderAtUtc,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? CancelledAtUtc,
    OrderCustomerDto Customer,
    IReadOnlyList<OrderLineDto> Lines,
    decimal TotalGross,
    decimal TotalVat,
    decimal TotalNet,
    string Currency,
    OrderPaymentDto? Payment,
    string? InvoiceNumber,
    IReadOnlyList<OrderVoucherDto> Vouchers,
    string AccessToken);

public sealed record OrderCustomerDto(
    string Name,
    string Email,
    string? Phone,
    string? CompanyName,
    string? CompanyId,
    string? VatId,
    string? CompanyAddress);

/// <summary>Pohyb na účtu, který objednávku zaplatil.</summary>
public sealed record OrderPaymentDto(
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? VariableSymbol,
    string? CounterpartyAccount,
    string? CounterpartyName);

/// <summary>Vystavený poukaz k objednávce. <c>Status</c> podle modulu Vouchers.</summary>
public sealed record OrderVoucherDto(
    string Code,
    string ProductCode,
    string Name,
    int Piece,
    string Status,
    string? Dedication);

/// <summary>Platba, která vypadá jako platba za objednávku, ale nesedí — k ručnímu vyřízení.</summary>
public sealed record UnmatchedPaymentDto(
    Guid Id,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? VariableSymbol,
    string? CounterpartyAccount,
    string? CounterpartyName,
    string? Message,
    string? Note);
