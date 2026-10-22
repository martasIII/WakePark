using System.Text.RegularExpressions;

using WakePark.BuildingBlocks.Domain;

namespace WakePark.Modules.Billing.Domain.Customers;

/// <summary>
/// Kontakt kupujícího zamrazený v objednávce — modul Customers se zatím neimplementuje
/// (ADR 0006). Firma, IČO, DIČ a sídlo jsou volitelné, kvůli dokladu na firmu — web je bere
/// z registru ARES, ne z formuláře.
/// </summary>
public sealed partial class CustomerContact
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 30;
    public const int AddressMaxLength = 300;

    private CustomerContact()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public string? CompanyName { get; private set; }

    /// <summary>IČO.</summary>
    public string? CompanyId { get; private set; }

    /// <summary>DIČ.</summary>
    public string? VatId { get; private set; }

    /// <summary>Sídlo firmy, jak ho uvádí ARES.</summary>
    public string? CompanyAddress { get; private set; }

    public static CustomerContact Create(
        string name,
        string email,
        string? phone = null,
        string? companyName = null,
        string? companyId = null,
        string? vatId = null,
        string? companyAddress = null)
    {
        string trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            throw new DomainException("customer.invalid_name", "Vyplňte jméno.");
        }

        string trimmedEmail = (email ?? string.Empty).Trim();
        if (trimmedEmail.Length > EmailMaxLength || !EmailPattern().IsMatch(trimmedEmail))
        {
            throw new DomainException("customer.invalid_email", "E-mail nevypadá platně.");
        }

        string? cleanPhone = Optional(phone);
        if (cleanPhone is not null && (cleanPhone.Length > PhoneMaxLength || !PhonePattern().IsMatch(cleanPhone)))
        {
            throw new DomainException("customer.invalid_phone", "Telefon smí obsahovat jen číslice, mezery a +.");
        }

        string? cleanCompanyId = Optional(companyId) is { } id ? ParseCompanyId(id) : null;

        string? cleanVatId = Optional(vatId)?.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (cleanVatId is not null && !VatIdPattern().IsMatch(cleanVatId))
        {
            throw new DomainException("customer.invalid_vat_id", "DIČ nemá platný tvar (např. CZ12345678).");
        }

        string? cleanCompanyName = Optional(companyName);
        if (cleanCompanyName is { Length: > NameMaxLength })
        {
            throw new DomainException("customer.invalid_company_name", "Název firmy je příliš dlouhý.");
        }

        string? cleanAddress = Optional(companyAddress);
        if (cleanAddress is { Length: > AddressMaxLength })
        {
            throw new DomainException("customer.invalid_company_address", "Adresa sídla je příliš dlouhá.");
        }

        if (cleanCompanyName is null && (cleanCompanyId is not null || cleanVatId is not null || cleanAddress is not null))
        {
            throw new DomainException("customer.company_name_required", "K IČO nebo DIČ vyplňte i název firmy.");
        }

        return new CustomerContact
        {
            Name = trimmedName,
            Email = trimmedEmail,
            Phone = cleanPhone,
            CompanyName = cleanCompanyName,
            CompanyId = cleanCompanyId,
            VatId = cleanVatId,
            CompanyAddress = cleanAddress,
        };
    }

    /// <summary>IČO bez mezer, nebo výjimka, když nemá platný tvar.</summary>
    public static string ParseCompanyId(string companyId)
    {
        ArgumentNullException.ThrowIfNull(companyId);

        string clean = companyId.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        return IsValidCompanyId(clean)
            ? clean
            : throw new DomainException("customer.invalid_company_id", "IČO nemá platný tvar (8 číslic s kontrolní číslicí).");
    }

    /// <summary>
    /// České IČO: 8 číslic, poslední je kontrolní (vážený součet modulo 11). Chytí překlepy
    /// dřív, než se zeptáme ARES.
    /// </summary>
    public static bool IsValidCompanyId(string companyId)
    {
        if (companyId.Length != 8 || !companyId.All(char.IsAsciiDigit))
        {
            return false;
        }

        int sum = 0;
        for (int i = 0; i < 7; i++)
        {
            sum += (companyId[i] - '0') * (8 - i);
        }

        int check = (11 - (sum % 11)) % 10;
        return companyId[7] - '0' == check;
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Záměrně volné: jedno @, něco před, doména s tečkou. Skutečné ověření je doručení e-mailu.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9 ]{6,}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    // CZ + 8 až 10 číslic (firmy 8, fyzické osoby 9–10).
    [GeneratedRegex("^CZ[0-9]{8,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex VatIdPattern();
}
