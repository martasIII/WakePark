using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Scheduling;
using WakePark.Modules.Operations.Domain.Seasons;

namespace WakePark.Modules.Operations.Infrastructure.Persistence.Repositories;

internal sealed class OperationsRepository(OperationsDbContext dbContext) : IOperationsRepository
{
    public async Task<Cable?> FindCableAsync(Guid cableId, CancellationToken cancellationToken = default) =>
        await dbContext.Cables
            .AsNoTracking()
            .FirstOrDefaultAsync(cable => cable.Id == cableId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Cable>> GetCablesAsync(
        Guid wakeParkId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Cables
            .AsNoTracking()
            .Where(cable => cable.WakeParkId == wakeParkId)
            .OrderBy(cable => cable.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<Season?> FindSeasonAsync(
        Guid cableId,
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        Guid? wakeParkId = await dbContext.Cables
            .AsNoTracking()
            .Where(cable => cable.Id == cableId)
            .Select(cable => (Guid?)cable.WakeParkId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (wakeParkId is null)
        {
            return null;
        }

        // Sezóna nastavená přímo pro vlek přebíjí obecnou sezónu areálu.
        return await dbContext.Seasons
            .AsNoTracking()
            .Where(season => season.WakeParkId == wakeParkId
                && (season.CableId == null || season.CableId == cableId)
                && season.StartDate <= localDate
                && season.EndDate >= localDate)
            .OrderByDescending(season => season.CableId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkingHours>> GetWorkingHoursAsync(
        Guid cableId,
        Guid seasonId,
        DayOfWeek dayOfWeek,
        CancellationToken cancellationToken = default) =>
        await dbContext.WorkingHours
            .AsNoTracking()
            .Where(hours => hours.CableId == cableId
                && hours.SeasonId == seasonId
                && hours.DayOfWeek == dayOfWeek)
            .OrderBy(hours => hours.StartTime)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ScheduleOverride?> FindScheduleOverrideAsync(
        Guid cableId,
        DateOnly localDate,
        CancellationToken cancellationToken = default) =>
        await dbContext.ScheduleOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(
                exception => exception.CableId == cableId && exception.Date == localDate,
                cancellationToken)
            .ConfigureAwait(false);
}
