using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.BuildingBlocks.Domain.ValueObjects;

namespace WakePark.Modules.Tenancy.Domain.Enterprises;

/// <summary>
/// Provozovatel — firma, která parky provozuje. Zároveň je to tenant celého systému:
/// jeho <see cref="Entity.Id"/> se propisuje do všech tenant-scoped entit.
/// </summary>
public sealed class Enterprise : AggregateRoot, IAuditable
{
    private Enterprise()
    {
    }

    private Enterprise(
        Guid id,
        string name,
        Address address,
        string registrationNumber,
        Guid? ownerUserAccountId)
        : base(id)
    {
        Name = name;
        Address = address;
        RegistrationNumber = registrationNumber;
        OwnerUserAccountId = ownerUserAccountId;
    }

    public string Name { get; private set; } = string.Empty;

    public Address Address { get; private set; } = null!;

    /// <summary>IČO.</summary>
    public string RegistrationNumber { get; private set; } = string.Empty;

    /// <summary>DIČ. Null u neplátce DPH.</summary>
    public string? VatNumber { get; private set; }

    public string? BankAccountNumber { get; private set; }

    public string? Iban { get; private set; }

    /// <summary>
    /// Vlastník. Odkaz do modulu Identity je záměrně jen holé Id — moduly si nesdílí navigační vlastnosti.
    /// Null u provozovatele založeného z výchozích dat (<c>provoz.json</c>) — doplní ho první
    /// admin (ADR 0009, upřesnění 24. 10.).
    /// </summary>
    public Guid? OwnerUserAccountId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Enterprise Create(
        string name,
        Address address,
        string registrationNumber,
        Guid ownerUserAccountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationNumber);

        if (ownerUserAccountId == Guid.Empty)
        {
            throw new DomainException("enterprise.owner_required", "Provozovatel musí mít vlastníka.");
        }

        return new Enterprise(NewId(), name.Trim(), address, registrationNumber.Trim(), ownerUserAccountId);
    }

    /// <summary>
    /// Provozovatel z výchozích dat: s pevným Id (na které už odkazuje katalog a objednávky)
    /// a zatím bez vlastníka.
    /// </summary>
    public static Enterprise CreateFromSetup(Guid id, string name, Address address, string registrationNumber)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("enterprise.id_required", "Provozovatel z výchozích dat musí mít Id.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationNumber);

        return new Enterprise(id, name.Trim(), address, registrationNumber.Trim(), null);
    }

    /// <summary>Údaje firmy podle výchozích dat nebo backoffice.</summary>
    public void Update(string name, Address address, string registrationNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationNumber);

        Name = name.Trim();
        Address = address;
        RegistrationNumber = registrationNumber.Trim();
    }

    /// <summary>Doplní vlastníka, pokud ještě žádný není. Existujícího nepřepíše.</summary>
    /// <returns><c>true</c>, pokud se vlastník nastavil.</returns>
    public bool ClaimOwnership(Guid userAccountId)
    {
        if (userAccountId == Guid.Empty)
        {
            throw new DomainException("enterprise.owner_required", "Provozovatel musí mít vlastníka.");
        }

        if (OwnerUserAccountId is not null)
        {
            return false;
        }

        OwnerUserAccountId = userAccountId;
        return true;
    }

    public void SetVatRegistration(string? vatNumber) =>
        VatNumber = string.IsNullOrWhiteSpace(vatNumber) ? null : vatNumber.Trim();

    public void SetBankDetails(string? bankAccountNumber, string? iban)
    {
        BankAccountNumber = string.IsNullOrWhiteSpace(bankAccountNumber) ? null : bankAccountNumber.Trim();
        Iban = string.IsNullOrWhiteSpace(iban) ? null : iban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }
}
