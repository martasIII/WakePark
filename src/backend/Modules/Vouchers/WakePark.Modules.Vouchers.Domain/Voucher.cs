using System.Security.Cryptography;

using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Vouchers.Domain;

/// <summary>
/// Poukaz — jeden kus zaplaceného produktu, který jde někomu darovat (ADR 0006). Vlastní
/// životní cyklus: vystaven → uplatněn / propadl, nezávisle na objednávce.
/// </summary>
/// <remarks>
/// Produkt, cena a platnost se **zamrazí** při vystavení — pozdější změna katalogu ani
/// sezóny už vystavený poukaz neovlivní. Co poukaz znamená na místě (sloty, jízdy), doplní
/// backoffice přes <see cref="ProductId"/>.
/// </remarks>
public sealed class Voucher : AggregateRoot, ITenantOwned, IAuditable
{
    /// <summary>Znaky kódu bez zaměnitelných (0/O, 1/I/L) — kód se diktuje do telefonu.</summary>
    public const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public const int CodeMaxLength = 20;

    private Voucher()
    {
    }

    private Voucher(Guid id)
        : base(id)
    {
    }

    public Guid EnterpriseId { get; private set; }

    /// <summary>Kód na poukazu, např. <c>WD-7KQ9-M3XT</c>. Podle něj se poukaz uplatní.</summary>
    public string Code { get; private set; } = string.Empty;

    public Guid OrderId { get; private set; }

    public long OrderNumber { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    /// <summary>Název česky.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Název v jazyce zákazníka — tak se vytiskne na poukaz.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public string Language { get; private set; } = string.Empty;

    /// <summary>Cena za kus včetně DPH, zamrazená z objednávky.</summary>
    public decimal Price { get; private set; }

    public decimal VatRate { get; private set; }

    /// <summary>Pořadí kusu v rámci položky objednávky (3 kusy = 1, 2, 3).</summary>
    public int Piece { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    public DateOnly ValidTo { get; private set; }

    public VoucherStatus Status { get; private set; }

    public DateTimeOffset IssuedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Voucher Issue(
        Guid enterpriseId,
        string code,
        Guid orderId,
        long orderNumber,
        VoucherProduct product,
        string language,
        int piece,
        DateOnly validFrom,
        DateOnly validTo,
        DateTimeOffset issuedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (validTo < validFrom)
        {
            throw new DomainException("voucher.invalid_validity", "Konec platnosti poukazu nesmí předcházet začátku.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(piece, 1);

        return new Voucher(NewId())
        {
            EnterpriseId = enterpriseId,
            Code = code,
            OrderId = orderId,
            OrderNumber = orderNumber,
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            Name = product.Name,
            DisplayName = product.DisplayName,
            Language = language,
            Price = product.Price,
            VatRate = product.VatRate,
            Piece = piece,
            ValidFrom = validFrom,
            ValidTo = validTo,
            Status = VoucherStatus.Issued,
            IssuedAtUtc = issuedAtUtc,
        };
    }

    /// <summary>
    /// Nový náhodný kód: prefix a 2 × 4 znaky, např. <c>WD-7KQ9-M3XT</c>. 31^8 ≈ 850 miliard
    /// kombinací — kód nejde uhodnout. Jedinečnost hlídá i unikátní index.
    /// </summary>
    public static string NewCode(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        Span<char> random = stackalloc char[8];
        for (int i = 0; i < random.Length; i++)
        {
            random[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        return $"{prefix}-{random[..4]}-{random[4..]}";
    }
}

/// <summary>Produkt, ze kterého poukaz vzniká — údaje z řádku zaplacené objednávky.</summary>
public sealed record VoucherProduct(Guid ProductId, string ProductCode, string Name, string DisplayName, decimal Price, decimal VatRate);

public enum VoucherStatus
{
    Issued,
    Redeemed,
    Cancelled,
}
