namespace WakePark.Modules.Operations.Application.Abstractions;

/// <summary>
/// Časová zóna areálu. Zóna je vlastnictvím modulu Tenancy, sem se dostává přes jeho kontrakt —
/// Operations nesahá na cizí entity.
/// </summary>
public interface IParkTimeZoneProvider
{
    Task<TimeZoneInfo> GetTimeZoneAsync(Guid wakeParkId, CancellationToken cancellationToken = default);
}
