namespace WakePark.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Poštovní adresa. Je to hodnotový objekt, ne entita — nikdy se nesdílí ani nedotazuje samostatně,
/// takže se do databáze vkládá jako sloupce hostitelské tabulky (EF <c>OwnsOne</c>).
/// </summary>
public sealed class Address : ValueObject
{
    private Address()
    {
    }

    public Address(string street, string? houseNumber, string city, string postalCode, string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        if (countryCode.Length != 2)
        {
            throw new ArgumentException("Kód země musí být dvouznakový podle ISO 3166-1 alpha-2.", nameof(countryCode));
        }

        Street = street.Trim();
        HouseNumber = houseNumber?.Trim();
        City = city.Trim();
        PostalCode = postalCode.Trim();
        CountryCode = countryCode.ToUpperInvariant();
    }

    public string Street { get; private init; } = string.Empty;

    public string? HouseNumber { get; private init; }

    public string City { get; private init; } = string.Empty;

    public string PostalCode { get; private init; } = string.Empty;

    public string CountryCode { get; private init; } = string.Empty;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return HouseNumber;
        yield return City;
        yield return PostalCode;
        yield return CountryCode;
    }
}
