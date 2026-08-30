using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Scheduling;
using WakePark.Modules.Operations.Domain.Seasons;

namespace WakePark.Modules.Operations.Application.Abstractions;

/// <summary>Přístup k rozvrhu vleku. Implementace je v infrastruktuře.</summary>
public interface IOperationsRepository
{
    Task<Cable?> FindCableAsync(Guid cableId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Cable>> GetCablesAsync(Guid wakeParkId, CancellationToken cancellationToken = default);

    /// <summary>Sezóna platná pro vlek k danému dni. Sezóna vázaná na vlek má přednost před obecnou.</summary>
    Task<Season?> FindSeasonAsync(Guid cableId, DateOnly localDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkingHours>> GetWorkingHoursAsync(
        Guid cableId,
        Guid seasonId,
        DayOfWeek dayOfWeek,
        CancellationToken cancellationToken = default);

    Task<ScheduleOverride?> FindScheduleOverrideAsync(
        Guid cableId,
        DateOnly localDate,
        CancellationToken cancellationToken = default);
}
