namespace WakePark.BuildingBlocks.Domain.ValueObjects;

/// <summary>Bod na mapě (WGS 84, jako v Google Maps nebo Mapy.cz).</summary>
public sealed class GeoPoint : ValueObject
{
    private GeoPoint()
    {
    }

    public GeoPoint(decimal latitude, decimal longitude)
    {
        if (latitude is < -90m or > 90m)
        {
            throw new DomainException("geo.invalid_latitude", "Zeměpisná šířka musí být mezi -90 a 90.");
        }

        if (longitude is < -180m or > 180m)
        {
            throw new DomainException("geo.invalid_longitude", "Zeměpisná délka musí být mezi -180 a 180.");
        }

        Latitude = decimal.Round(latitude, 6);
        Longitude = decimal.Round(longitude, 6);
    }

    public decimal Latitude { get; private init; }

    public decimal Longitude { get; private init; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }
}
