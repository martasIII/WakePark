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

    /// <summary>Náhodná část kódu jízdního poukazu — jen písmena, bez zaměnitelných I, L, O.</summary>
    public const string RideCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ";

    public const int RideCodeRandomLength = 5;

    /// <summary>Nejdelší je kód permanentky s vybavením: <c>PD2027-20x60-VV-KXRTM</c> (21 znaků).</summary>
    public const int CodeMaxLength = 24;

    /// <summary>Stejné jako v objednávce — tam se věnování ověřuje, sem se jen zamrazí.</summary>
    public const int DedicationMaxLength = 120;

    private Voucher()
    {
    }

    private Voucher(Guid id)
        : base(id)
    {
    }

    public Guid EnterpriseId { get; private set; }

    /// <summary>
    /// Kód na poukazu, např. <c>V40-2027-KXRTM</c> (jízdy), <c>PD2027-10x20-KXRTM</c> (permanentky)
    /// nebo <c>WD-7KQ9-M3XT</c> (ostatní). Podle něj se poukaz uplatní.
    /// </summary>
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

    /// <summary>Věnování z objednávky, vytiskne se na poukaz. <c>null</c> = bez věnování.</summary>
    public string? Dedication { get; private set; }

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
        string? dedication,
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

        if (dedication?.Length > DedicationMaxLength)
        {
            throw new DomainException("voucher.dedication_too_long", $"Věnování může mít nejvýš {DedicationMaxLength} znaků.");
        }

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
            Dedication = string.IsNullOrWhiteSpace(dedication) ? null : dedication,
            ValidFrom = validFrom,
            ValidTo = validTo,
            Status = VoucherStatus.Issued,
            IssuedAtUtc = issuedAtUtc,
        };
    }

    /// <summary>
    /// Kód jízdního poukazu <c>V{typ}-{rok sezóny}-{5 písmen}</c>, např. <c>V40-2027-KXRTM</c>.
    /// Typ i rok jsou z kódu vidět, náhodných je jen 5 písmen: 23^5 ≈ 6,4 milionu kombinací na
    /// typ a rok — při stovkách prodaných kusů je šance uhodnout platný kód zhruba 1 : 20 000.
    /// Stačí to jen proto, že se kód ověřuje u pokladny; veřejné ověření kódu přes web by
    /// potřebovalo omezení počtu pokusů. Jedinečnost hlídá unikátní index.
    /// </summary>
    public static string NewRideCode(string type, int seasonYear)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentOutOfRangeException.ThrowIfLessThan(seasonYear, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(seasonYear, 9999);

        return $"V{type}-{seasonYear}-{RandomLetters()}";
    }

    /// <summary>
    /// Kód permanentky <c>PD{rok sezóny}-{typ}-{5 písmen}</c>, např. <c>PD2027-10x20-KXRTM</c>
    /// nebo s vybavením <c>PD2027-20x60-VV-KXRTM</c>. Odhadnutelnost stejná jako u jízdního
    /// poukazu — ověřuje se u pokladny, jízdy se odepisují v systému (zákazník si je na kartě
    /// může odškrtávat jen pro sebe).
    /// </summary>
    public static string NewPassCode(string type, int seasonYear)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentOutOfRangeException.ThrowIfLessThan(seasonYear, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(seasonYear, 9999);

        return $"PD{seasonYear}-{type}-{RandomLetters()}";
    }

    private static string RandomLetters()
    {
        Span<char> random = stackalloc char[RideCodeRandomLength];
        for (int i = 0; i < random.Length; i++)
        {
            random[i] = RideCodeAlphabet[RandomNumberGenerator.GetInt32(RideCodeAlphabet.Length)];
        }

        return random.ToString();
    }

    /// <summary>
    /// Kód ostatních poukazů (produkt bez typu jízdy ani permanentky): prefix a 2 × 4 znaky, např. <c>WD-7KQ9-M3XT</c>. 31^8 ≈ 850 miliard
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
