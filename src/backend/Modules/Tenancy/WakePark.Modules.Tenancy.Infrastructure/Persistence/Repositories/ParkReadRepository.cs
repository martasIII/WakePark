using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Tenancy.Application.Abstractions;
using WakePark.Modules.Tenancy.Contracts;
using WakePark.Modules.Tenancy.Domain.Parks;

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence.Repositories;

internal sealed class ParkReadRepository(TenancyDbContext dbContext) : IParkReadRepository
{
    /// <summary>
    /// Jedna projekce pro všechny dotazy. Kdyby se opisovala, dřív nebo později se rozejdou
    /// a jeden endpoint začne vracet něco jiného než druhý.
    /// </summary>
    private static readonly Expression<Func<Park, ParkDto>> ToDto = park => new ParkDto(
        park.Id,
        park.EnterpriseId,
        park.Name,
        park.Description,
        park.Address.City,
        park.Address.CountryCode,
        park.TimeZoneId,
        park.Currency,
        new CancellationPolicyDto(
            park.CancellationPolicy.CancellationAllowed,
            park.CancellationPolicy.FreeCancellationHours,
            park.CancellationPolicy.LateCancellationRefundPercent,
            park.CancellationPolicy.NoShowRefundPercent));

    public async Task<IReadOnlyList<ParkDto>> GetByEnterpriseAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Parks
            .AsNoTracking()
            .Where(park => park.EnterpriseId == enterpriseId)
            .OrderBy(park => park.Name)
            .Select(ToDto)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ParkDto?> GetByIdAsync(Guid parkId, CancellationToken cancellationToken = default) =>
        await dbContext.Parks
            .AsNoTracking()
            .Where(park => park.Id == parkId)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
}
