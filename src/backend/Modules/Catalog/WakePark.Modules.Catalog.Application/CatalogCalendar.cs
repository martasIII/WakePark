using WakePark.BuildingBlocks.Application.Abstractions;

namespace WakePark.Modules.Catalog.Application;

/// <summary>Dnešní datum v čase areálu — podle něj platí ceny.</summary>
internal sealed class CatalogCalendar(IClock clock, CatalogOptions options)
{
    public DateOnly Today =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId)).DateTime);
}
