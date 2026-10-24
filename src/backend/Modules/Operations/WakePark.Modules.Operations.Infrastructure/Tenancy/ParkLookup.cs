using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Operations.Infrastructure.Tenancy;

/// <summary>Areál přes veřejný kontrakt modulu Tenancy.</summary>
internal sealed class ParkLookup(IParkDirectory parkDirectory) : IParkLookup
{
    public async Task<ParkInfo?> FindAsync(Guid wakeParkId, CancellationToken cancellationToken = default)
    {
        ParkDto? park = await parkDirectory.GetParkAsync(wakeParkId, cancellationToken).ConfigureAwait(false);

        return park is not null && TimeZoneInfo.TryFindSystemTimeZoneById(park.TimeZoneId, out TimeZoneInfo? timeZone)
            ? new ParkInfo(park.Id, park.EnterpriseId, timeZone)
            : null;
    }
}
