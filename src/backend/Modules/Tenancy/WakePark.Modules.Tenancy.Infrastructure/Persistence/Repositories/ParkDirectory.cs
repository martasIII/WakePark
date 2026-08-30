using WakePark.Modules.Tenancy.Application.Abstractions;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementace veřejného adresáře areálů. Je to jen tenká vrstva nad čtecím repozitářem —
/// smysl má v tom, že ostatní moduly vidí kontrakt, ne vnitřek modulu Tenancy.
/// </summary>
internal sealed class ParkDirectory(IParkReadRepository repository) : IParkDirectory
{
    public Task<ParkDto?> GetParkAsync(Guid parkId, CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(parkId, cancellationToken);

    public Task<IReadOnlyList<ParkDto>> GetParksAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(enterpriseId, cancellationToken);
}
