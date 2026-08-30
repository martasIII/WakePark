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
        Guid ownerUserAccountId)
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
    /// </summary>
    public Guid OwnerUserAccountId { get; private set; }

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

    public void SetVatRegistration(string? vatNumber) =>
        VatNumber = string.IsNullOrWhiteSpace(vatNumber) ? null : vatNumber.Trim();

    public void SetBankDetails(string? bankAccountNumber, string? iban)
    {
        BankAccountNumber = string.IsNullOrWhiteSpace(bankAccountNumber) ? null : bankAccountNumber.Trim();
        Iban = string.IsNullOrWhiteSpace(iban) ? null : iban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }
}
