using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Application.Abstractions;

/// <summary>
/// Čtecí přístup k areálům. Rozhraní je v aplikační vrstvě, implementace v infrastruktuře —
/// aplikační kód tak nezná EF Core ani konkrétní databázi.
/// </summary>
/// <remarks>
/// Repozitář o přihlášeném uživateli nic neví; na provozovatele omezují dotazy aplikační
/// vrstvy (ADR 0007). Záměrně tu není „všechny areály napříč provozovateli“ —
/// co neexistuje, nejde omylem použít.
/// </remarks>
public interface IParkReadRepository
{
    Task<IReadOnlyList<ParkDto>> GetByEnterpriseAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default);

    Task<ParkDto?> GetByIdAsync(Guid parkId, CancellationToken cancellationToken = default);
}
