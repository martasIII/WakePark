using Microsoft.Extensions.Caching.Memory;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Operations.Infrastructure.Tenancy;

/// <summary>
/// Zjišťuje časovou zónu areálu přes veřejný kontrakt modulu Tenancy.
/// Výsledek se drží v paměti — zóna se u areálu prakticky nemění a plánování slotů se na ni
/// ptá při každém dotazu na rozvrh.
/// </summary>
internal sealed class ParkTimeZoneProvider(IParkDirectory parkDirectory, IMemoryCache cache)
    : IParkTimeZoneProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<TimeZoneInfo> GetTimeZoneAsync(
        Guid wakeParkId,
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue($"park-tz:{wakeParkId}", out TimeZoneInfo? cached) && cached is not null)
        {
            return cached;
        }

        ParkDto park = await parkDirectory.GetParkAsync(wakeParkId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("park.not_found", "Areál neexistuje.");

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(park.TimeZoneId, out TimeZoneInfo? timeZone))
        {
            throw new DomainException(
                "park.unknown_timezone",
                $"Areál má nastavenou neznámou časovou zónu '{park.TimeZoneId}'.");
        }

        cache.Set($"park-tz:{wakeParkId}", timeZone, CacheDuration);

        return timeZone;
    }
}
