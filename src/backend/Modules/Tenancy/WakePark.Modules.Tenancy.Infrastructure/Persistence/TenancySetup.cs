using Microsoft.EntityFrameworkCore;

using WakePark.BuildingBlocks.Domain.ValueObjects;
using WakePark.Modules.Tenancy.Domain.Enterprises;
using WakePark.Modules.Tenancy.Domain.Parks;

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence;

/// <summary>Provozovatel z výchozích dat (<c>provoz.json</c>).</summary>
public sealed record EnterpriseSetup(
    Guid Id,
    string Name,
    string RegistrationNumber,
    string? VatNumber,
    AddressSetup Address,
    string? BankAccountNumber,
    string? Iban);

/// <summary>Areál z výchozích dat. Poloha je GPS — přesnou poštovní adresu areál nemá.</summary>
public sealed record ParkSetup(
    Guid Id,
    string Name,
    string? Description,
    AddressSetup Address,
    string TimeZoneId,
    string Currency,
    string? ReservationEmail,
    decimal? Latitude,
    decimal? Longitude);

public sealed record AddressSetup(string Street, string? HouseNumber, string City, string PostalCode, string CountryCode)
{
    public Address ToAddress() => new(Street, HouseNumber, City, PostalCode, CountryCode);
}

/// <summary>
/// Srovná provozovatele a areál s výchozími daty (ADR 0009, upřesnění 24. 10.): co chybí,
/// založí, existující přepíše podle souboru. Vlastníka provozovatele nemění — toho doplní
/// první admin (<see cref="ClaimOwnershipAsync"/>).
/// </summary>
/// <remarks>Stejně jako seed identity je to krok nasazení, ne běžný provoz aplikace.</remarks>
public sealed class TenancySetupApplier(TenancyDbContext dbContext)
{
    public async Task ApplyAsync(EnterpriseSetup enterpriseSetup, ParkSetup parkSetup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(enterpriseSetup);
        ArgumentNullException.ThrowIfNull(parkSetup);

        Enterprise? enterprise = await dbContext.Enterprises
            .FirstOrDefaultAsync(e => e.Id == enterpriseSetup.Id, cancellationToken)
            .ConfigureAwait(false);

        if (enterprise is null)
        {
            enterprise = Enterprise.CreateFromSetup(
                enterpriseSetup.Id, enterpriseSetup.Name, enterpriseSetup.Address.ToAddress(), enterpriseSetup.RegistrationNumber);
            dbContext.Enterprises.Add(enterprise);
        }
        else
        {
            enterprise.Update(enterpriseSetup.Name, enterpriseSetup.Address.ToAddress(), enterpriseSetup.RegistrationNumber);
        }

        enterprise.SetVatRegistration(enterpriseSetup.VatNumber);
        enterprise.SetBankDetails(enterpriseSetup.BankAccountNumber, enterpriseSetup.Iban);

        Park? park = await dbContext.Parks
            .FirstOrDefaultAsync(p => p.Id == parkSetup.Id, cancellationToken)
            .ConfigureAwait(false);

        if (park is null)
        {
            park = Park.CreateFromSetup(
                parkSetup.Id, enterprise.Id, parkSetup.Name, parkSetup.Address.ToAddress(), parkSetup.TimeZoneId, parkSetup.Currency);
            dbContext.Parks.Add(park);
        }
        else
        {
            park.Update(parkSetup.Name, parkSetup.Address.ToAddress(), parkSetup.TimeZoneId, parkSetup.Currency);
        }

        park.Describe(parkSetup.Description);
        park.SetReservationEmail(parkSetup.ReservationEmail);
        park.Locate(parkSetup.Latitude is { } lat && parkSetup.Longitude is { } lon ? new GeoPoint(lat, lon) : null);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Doplní provozovateli vlastníka, pokud ještě žádného nemá.</summary>
    /// <returns><c>true</c>, pokud se vlastník nastavil.</returns>
    public async Task<bool> ClaimOwnershipAsync(Guid enterpriseId, Guid userAccountId, CancellationToken cancellationToken = default)
    {
        Enterprise? enterprise = await dbContext.Enterprises
            .FirstOrDefaultAsync(e => e.Id == enterpriseId, cancellationToken)
            .ConfigureAwait(false);

        if (enterprise is null || !enterprise.ClaimOwnership(userAccountId))
        {
            return false;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
