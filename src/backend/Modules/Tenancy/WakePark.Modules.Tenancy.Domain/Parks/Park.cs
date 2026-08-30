using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.BuildingBlocks.Domain.ValueObjects;

namespace WakePark.Modules.Tenancy.Domain.Parks;

/// <summary>
/// Areál wakeparku. V dokumentaci datového modelu vystupuje jako <c>WakePark</c>; třída se jmenuje
/// <c>Park</c>, protože typ pojmenovaný stejně jako kořenový namespace by rozbil rozlišení jmen v C#.
/// Tabulka v databázi se jmenuje <c>wake_parks</c>.
/// </summary>
public sealed class Park : AggregateRoot, IAuditable, ITenantOwned
{
    private Park()
    {
    }

    private Park(
        Guid id,
        Guid enterpriseId,
        string name,
        Address address,
        string timeZoneId,
        string currency)
        : base(id)
    {
        EnterpriseId = enterpriseId;
        Name = name;
        Address = address;
        TimeZoneId = timeZoneId;
        Currency = currency;
    }

    public Guid EnterpriseId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Address Address { get; private set; } = null!;

    /// <summary>
    /// IANA identifikátor zóny, např. <c>Europe/Prague</c>. Bez něj nelze převést provozní dobu
    /// (lokální čas) na okamžik slotu (UTC) přes přechod na letní čas.
    /// </summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    /// <summary>Výchozí měna dokladů, ISO 4217.</summary>
    public string Currency { get; private set; } = string.Empty;

    public string? ReservationEmail { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Park Create(
        Guid enterpriseId,
        string name,
        Address address,
        string timeZoneId,
        string currency)
    {
        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("park.enterprise_required", "Areál musí patřit provozovateli.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (currency.Length != 3)
        {
            throw new DomainException("park.invalid_currency", "Měna musí být třípísmenný kód podle ISO 4217.");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            throw new DomainException("park.unknown_timezone", $"Neznámá časová zóna '{timeZoneId}'.");
        }

        return new Park(NewId(), enterpriseId, name.Trim(), address, timeZoneId, currency.ToUpperInvariant());
    }

    public void Describe(string? description) =>
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public void SetReservationEmail(string? email) =>
        ReservationEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
