using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Application.Abstractions;

/// <summary>
/// Čtecí přístup k areálům. Rozhraní je v aplikační vrstvě, implementace v infrastruktuře —
/// aplikační kód tak nezná EF Core ani konkrétní databázi.
/// </summary>
public interface IParkReadRepository
{
    Task<IReadOnlyList<ParkDto>> GetAllAsync(Guid enterpriseId, CancellationToken cancellationToken = default);

    Task<ParkDto?> GetByIdAsync(Guid parkId, CancellationToken cancellationToken = default);
}
