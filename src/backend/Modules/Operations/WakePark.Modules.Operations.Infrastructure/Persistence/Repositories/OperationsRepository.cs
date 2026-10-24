using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Calendar;
using WakePark.Modules.Operations.Domain.Parks;

namespace WakePark.Modules.Operations.Infrastructure.Persistence.Repositories;

/// <summary>
/// Záznamy se načítají se sledováním změn — příkazy je upravují a dat je málo (vleky, dny
/// jedné sezóny). Čtení pro rozvrh tak nepotřebuje zvláštní repozitář.
/// </summary>
internal sealed class OperationsRepository(OperationsDbContext dbContext) : IOperationsRepository
{
    public Task<Cable?> FindCableAsync(Guid cableId, CancellationToken cancellationToken = default) =>
        dbContext.Cables.FirstOrDefaultAsync(cable => cable.Id == cableId, cancellationToken);

    public async Task<IReadOnlyList<Cable>> GetCablesAsync(Guid wakeParkId, CancellationToken cancellationToken = default) =>
        await dbContext.Cables
            .Where(cable => cable.WakeParkId == wakeParkId)
            .OrderBy(cable => cable.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ParkSettings?> GetSettingsAsync(Guid wakeParkId, CancellationToken cancellationToken = default) =>
        dbContext.ParkSettings.FirstOrDefaultAsync(settings => settings.WakeParkId == wakeParkId, cancellationToken);

    public async Task<IReadOnlyList<OperatingDay>> GetOperatingDaysAsync(
        Guid wakeParkId,
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken = default) =>
        await dbContext.OperatingDays
            .Where(day => day.WakeParkId == wakeParkId && day.Date >= firstDay && day.Date <= lastDay)
            .OrderBy(day => day.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Blocking>> GetBlockingsAsync(
        Guid wakeParkId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.Blockings
            .Where(blocking => blocking.WakeParkId == wakeParkId && blocking.StartUtc < toUtc && blocking.EndUtc > fromUtc)
            .OrderBy(blocking => blocking.StartUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<Blocking?> FindBlockingAsync(Guid blockingId, CancellationToken cancellationToken = default) =>
        dbContext.Blockings.FirstOrDefaultAsync(blocking => blocking.Id == blockingId, cancellationToken);

    public void Add(Cable cable) => dbContext.Cables.Add(cable);

    public void Add(ParkSettings settings) => dbContext.ParkSettings.Add(settings);

    public void Add(OperatingDay day) => dbContext.OperatingDays.Add(day);

    public void Add(Blocking blocking) => dbContext.Blockings.Add(blocking);

    public void Remove(OperatingDay day) => dbContext.OperatingDays.Remove(day);

    public void Remove(Blocking blocking) => dbContext.Blockings.Remove(blocking);
}
