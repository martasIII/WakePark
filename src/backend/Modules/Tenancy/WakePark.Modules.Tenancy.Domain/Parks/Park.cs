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
        CancellationPolicy = CancellationPolicy.Default;
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

    /// <summary>Poloha areálu (GPS). Přesnou poštovní adresu areál Džbán nemá.</summary>
    public GeoPoint? Location { get; private set; }

    /// <summary>Pravidla storna. Každý areál si je nastavuje sám.</summary>
    public CancellationPolicy CancellationPolicy { get; private set; } = CancellationPolicy.Default;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Park Create(
        Guid enterpriseId,
        string name,
        Address address,
        string timeZoneId,
        string currency) =>
        Build(NewId(), enterpriseId, name, address, timeZoneId, currency);

    private static Park Build(
        Guid id,
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

        return new Park(id, enterpriseId, name.Trim(), address, timeZoneId, currency.ToUpperInvariant());
    }

    /// <summary>Areál z výchozích dat s pevným Id — web podle něj čte otevírací dobu.</summary>
    public static Park CreateFromSetup(Guid id, Guid enterpriseId, string name, Address address, string timeZoneId, string currency)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("park.id_required", "Areál z výchozích dat musí mít Id.");
        }

        return Build(id, enterpriseId, name, address, timeZoneId, currency);
    }

    /// <summary>Údaje areálu podle výchozích dat nebo backoffice. Provozovatele změnit nejde.</summary>
    public void Update(string name, Address address, string timeZoneId, string currency)
    {
        Park validated = Build(Id, EnterpriseId, name, address, timeZoneId, currency);

        Name = validated.Name;
        Address = validated.Address;
        TimeZoneId = validated.TimeZoneId;
        Currency = validated.Currency;
    }

    public void Locate(GeoPoint? location) => Location = location;

    public void Describe(string? description) =>
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public void SetReservationEmail(string? email) =>
        ReservationEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    public void SetCancellationPolicy(CancellationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        CancellationPolicy = policy;
    }
}
