using WakePark.BuildingBlocks.Application;
using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Operations.Application.Abstractions;

namespace WakePark.Modules.Operations.Application.Calendar;

/// <summary>
/// Areál, se kterým se pracuje, a „dnes“ v jeho čase. Pro backoffice ověří, že areál patří
/// provozovateli z přihlášení (ADR 0007): oprávnění pro celého provozovatele by jinak pustilo
/// i k cizímu areálu, kdyby někdo podstrčil jeho Id. Cizí areál vypadá jako neexistující.
/// </summary>
internal sealed class ParkAccess(IParkLookup lookup, ITenantContext tenant, IClock clock)
{
    /// <summary>Areál přihlášeného provozovatele, jinak 404.</summary>
    public async Task<ParkInfo> ForTenantAsync(Guid wakeParkId, CancellationToken cancellationToken)
    {
        ParkInfo? park = await lookup.FindAsync(wakeParkId, cancellationToken).ConfigureAwait(false);

        return park is not null && tenant.EnterpriseId is { } enterpriseId && park.EnterpriseId == enterpriseId
            ? park
            : throw new NotFoundException("Areál nenalezen.");
    }

    /// <summary>Areál pro veřejné čtení (otevírací doba je veřejná), jinak 404.</summary>
    public async Task<ParkInfo> PublicAsync(Guid wakeParkId, CancellationToken cancellationToken) =>
        await lookup.FindAsync(wakeParkId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Areál nenalezen.");

    public DateOnly Today(ParkInfo park) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, park.TimeZone).DateTime);

    /// <summary>Okamžik začátku dne v čase areálu (půlnoc lokálně) jako UTC.</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Půlnoc v Evropě nikdy nespadá do posunu času, ale pro jistotu se neexistující čas posune.
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
    }
}
