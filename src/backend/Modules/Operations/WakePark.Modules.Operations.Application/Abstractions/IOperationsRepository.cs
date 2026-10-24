using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Calendar;
using WakePark.Modules.Operations.Domain.Parks;

namespace WakePark.Modules.Operations.Application.Abstractions;

/// <summary>Vleky, nastavení areálu, provozní dny a blokace. Implementace je v infrastruktuře.</summary>
public interface IOperationsRepository
{
    Task<Cable?> FindCableAsync(Guid cableId, CancellationToken cancellationToken = default);

    /// <summary>Všechny vleky areálu včetně neaktivních, seřazené podle názvu.</summary>
    Task<IReadOnlyList<Cable>> GetCablesAsync(Guid wakeParkId, CancellationToken cancellationToken = default);

    Task<ParkSettings?> GetSettingsAsync(Guid wakeParkId, CancellationToken cancellationToken = default);

    /// <summary>Provozní dny areálu v rozsahu dat, obě meze včetně.</summary>
    Task<IReadOnlyList<OperatingDay>> GetOperatingDaysAsync(Guid wakeParkId, DateOnly firstDay, DateOnly lastDay, CancellationToken cancellationToken = default);

    /// <summary>Blokace areálu, které se překrývají s intervalem [fromUtc, toUtc).</summary>
    Task<IReadOnlyList<Blocking>> GetBlockingsAsync(Guid wakeParkId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<Blocking?> FindBlockingAsync(Guid blockingId, CancellationToken cancellationToken = default);

    void Add(Cable cable);

    void Add(ParkSettings settings);

    void Add(OperatingDay day);

    void Add(Blocking blocking);

    void Remove(OperatingDay day);

    void Remove(Blocking blocking);
}

/// <inheritdoc cref="IUnitOfWork" />
/// <remarks>Vlastní rozhraní pro každý modul — sdílené by v jednom DI kontejneru přepsal poslední modul.</remarks>
public interface IOperationsUnitOfWork : IUnitOfWork;
