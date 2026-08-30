using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Application.Abstractions;

/// <summary>
/// Čtecí přístup k areálům. Rozhraní je v aplikační vrstvě, implementace v infrastruktuře —
/// aplikační kód tak nezná EF Core ani konkrétní databázi.
/// </summary>
public interface IParkReadRepository
{
    /// <summary>
    /// Všechny areály napříč provozovateli.
    /// </summary>
    /// <remarks>
    /// Až přibude autentizace, musí se výsledek omezit na tenanta přihlášeného uživatele.
    /// Dokud modul Identity neexistuje, je to otevřený seznam.
    /// </remarks>
    Task<IReadOnlyList<ParkDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParkDto>> GetByEnterpriseAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default);

    Task<ParkDto?> GetByIdAsync(Guid parkId, CancellationToken cancellationToken = default);
}
