namespace WakePark.Modules.Operations.Application.Abstractions;

/// <summary>
/// Časová zóna areálu. Zóna je vlastnictvím modulu Tenancy, sem se dostává přes jeho kontrakt —
/// Operations nesahá na cizí entity.
/// </summary>
public interface IParkTimeZoneProvider
{
    Task<TimeZoneInfo> GetTimeZoneAsync(Guid wakeParkId, CancellationToken cancellationToken = default);
}

/// <summary>Areál z modulu Tenancy: komu patří a v jakém je časovém pásmu.</summary>
public sealed record ParkInfo(Guid Id, Guid EnterpriseId, TimeZoneInfo TimeZone);

/// <summary>Vyhledání areálu přes kontrakt modulu Tenancy.</summary>
public interface IParkLookup
{
    /// <returns>Null, když areál neexistuje.</returns>
    Task<ParkInfo?> FindAsync(Guid wakeParkId, CancellationToken cancellationToken = default);
}
