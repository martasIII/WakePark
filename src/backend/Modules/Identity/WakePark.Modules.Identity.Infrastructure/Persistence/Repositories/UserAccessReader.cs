using Microsoft.EntityFrameworkCore;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Contracts;
using WakePark.Modules.Identity.Domain.Authorization;

namespace WakePark.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Poskládá přístupy uživatele: platné role → jejich oprávnění → seskupeno podle areálu.
/// </summary>
internal sealed class UserAccessReader(IdentityDbContext dbContext, IClock clock) : IUserAccessReader
{
    public async Task<CurrentUserDto?> GetAccessAsync(
        Guid userAccountId,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(a => a.Id == userAccountId)
            .Select(a => new { a.Id, a.Email })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return null;
        }

        DateTimeOffset now = clock.UtcNow;

        // Jedním dotazem: platná přiřazení rolí i s kódy oprávnění, která role nese.
        var grants = await (
            from assignment in dbContext.UserParkRoles.AsNoTracking()
            where assignment.UserAccountId == userAccountId
                && assignment.ValidFromUtc <= now
                && (assignment.ValidToUtc == null || assignment.ValidToUtc > now)
            join role in dbContext.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            select new
            {
                assignment.EnterpriseId,
                assignment.WakeParkId,
                RoleName = role.Name,
                Permissions = dbContext.Set<RolePermission>()
                    .Where(rp => rp.RoleId == role.Id)
                    .Join(dbContext.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Code)
                    .ToList(),
            }).ToListAsync(cancellationToken).ConfigureAwait(false);

        Guid? enterpriseId = grants.Count > 0 ? grants[0].EnterpriseId : null;

        // Role bez areálu platí všude u daného provozovatele.
        IReadOnlyList<string> globalPermissions =
        [
            .. grants
                .Where(g => g.WakeParkId is null)
                .SelectMany(g => g.Permissions)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        ];

        IReadOnlyList<ParkAccessDto> parks =
        [
            .. grants
                .Where(g => g.WakeParkId is not null)
                .GroupBy(g => g.WakeParkId!.Value)
                .Select(group => new ParkAccessDto(
                    group.Key,
                    [.. group.Select(g => g.RoleName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                    [.. group.SelectMany(g => g.Permissions).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]))
                .OrderBy(park => park.WakeParkId)
        ];

        return new CurrentUserDto(account.Id, account.Email, enterpriseId, parks, globalPermissions);
    }
}
