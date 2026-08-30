using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Tenancy.Application.Abstractions;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence.Repositories;

internal sealed class ParkReadRepository(TenancyDbContext dbContext) : IParkReadRepository
{
    public async Task<IReadOnlyList<ParkDto>> GetAllAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Parks
            .AsNoTracking()
            .Where(p => p.EnterpriseId == enterpriseId)
            .OrderBy(p => p.Name)
            .Select(p => new ParkDto(
                p.Id,
                p.EnterpriseId,
                p.Name,
                p.Description,
                p.Address.City,
                p.Address.CountryCode,
                p.TimeZoneId,
                p.Currency))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ParkDto?> GetByIdAsync(Guid parkId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Parks
            .AsNoTracking()
            .Where(p => p.Id == parkId)
            .Select(p => new ParkDto(
                p.Id,
                p.EnterpriseId,
                p.Name,
                p.Description,
                p.Address.City,
                p.Address.CountryCode,
                p.TimeZoneId,
                p.Currency))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
