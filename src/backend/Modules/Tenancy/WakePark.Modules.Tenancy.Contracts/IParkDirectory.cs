namespace WakePark.Modules.Tenancy.Contracts;

/// <summary>
/// Čtení areálů pro ostatní moduly. Jediná cesta, jak se odjinud dostat k údajům o areálu —
/// entity modulu Tenancy zůstávají uvnitř.
/// </summary>
public interface IParkDirectory
{
    Task<ParkDto?> GetParkAsync(Guid parkId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParkDto>> GetParksAsync(Guid enterpriseId, CancellationToken cancellationToken = default);
}
