using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Contracts;
using WakePark.Modules.Identity.Domain.Authorization;
using WakePark.Modules.Identity.Domain.Users;

namespace WakePark.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Doplní oprávnění a systémové role podle konstant v kódu a volitelně založí prvního správce.
/// </summary>
/// <remarks>
/// Zdrojem pravdy je <see cref="Permissions"/> v kódu, tabulka je jen zrcadlo pro administraci.
/// Seeder je proto idempotentní — přidává, co chybí, a nikdy nic nemaže.
/// </remarks>
public sealed partial class IdentitySeeder(
    IdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IClock clock,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(
        string? bootstrapAdminEmail,
        string? bootstrapAdminPassword,
        Guid? bootstrapEnterpriseId,
        CancellationToken cancellationToken = default)
    {
        await SeedPermissionsAsync(cancellationToken).ConfigureAwait(false);
        await SeedRolesAsync(cancellationToken).ConfigureAwait(false);
        await SeedAdminAsync(bootstrapAdminEmail, bootstrapAdminPassword, bootstrapEnterpriseId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        HashSet<string> existing = [.. await dbContext.Permissions
            .Select(p => p.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

        foreach ((string code, string description) in Permissions.All)
        {
            if (existing.Add(code))
            {
                dbContext.Permissions.Add(new Permission(code, description));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, Guid> permissionIds = await dbContext.Permissions
            .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        foreach ((string roleName, IReadOnlyList<string> grants) in SystemRoles.Grants)
        {
            Role? role = await dbContext.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.EnterpriseId == null && r.Name == roleName, cancellationToken)
                .ConfigureAwait(false);

            if (role is null)
            {
                role = Role.System(roleName, DescribeRole(roleName));
                dbContext.Roles.Add(role);
            }

            foreach (string code in grants)
            {
                if (permissionIds.TryGetValue(code, out Guid permissionId))
                {
                    role.Grant(permissionId);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedAdminAsync(
        string? email,
        string? password,
        Guid? enterpriseId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            LogNoBootstrapAdmin(logger);
            return;
        }

        string normalized = UserAccount.NormalizeEmail(email);

        if (await dbContext.UserAccounts.AnyAsync(a => a.Email == normalized, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        UserAccount admin = UserAccount.Create(normalized, passwordHasher.Hash(password));
        admin.ConfirmEmail(clock.UtcNow);
        dbContext.UserAccounts.Add(admin);

        if (enterpriseId is { } tenant)
        {
            Role adminRole = await dbContext.Roles
                .FirstAsync(r => r.EnterpriseId == null && r.Name == SystemRoles.Admin, cancellationToken)
                .ConfigureAwait(false);

            // Role bez areálu: správce musí mít přístup dřív, než vůbec nějaký areál vznikne.
            dbContext.UserParkRoles.Add(
                UserParkRole.ForWholeEnterprise(admin.Id, tenant, adminRole.Id, clock.UtcNow));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogAdminCreated(logger, normalized);
    }

    /// <summary>
    /// Admin pro celého provozovatele (příkaz <c>create-admin</c> na serveru, ADR 0009). Opakovatelné:
    /// existujícímu účtu nastaví nové heslo a odemkne ho — tudy se admin dostane zpátky, když
    /// heslo zapomene nebo si účet zamkne špatnými pokusy.
    /// </summary>
    /// <returns>Id účtu.</returns>
    public async Task<Guid> EnsureAdminAsync(string email, string password, Guid enterpriseId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await SeedPermissionsAsync(cancellationToken).ConfigureAwait(false);
        await SeedRolesAsync(cancellationToken).ConfigureAwait(false);

        string normalized = UserAccount.NormalizeEmail(email);
        UserAccount? account = await dbContext.UserAccounts
            .FirstOrDefaultAsync(a => a.Email == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            account = UserAccount.Create(normalized, passwordHasher.Hash(password));
            account.ConfirmEmail(clock.UtcNow);
            dbContext.UserAccounts.Add(account);
            LogAdminCreated(logger, normalized);
        }
        else
        {
            // Nové heslo zároveň zneplatní všechna dosavadní přihlášení (security stamp).
            account.SetPassword(passwordHasher.Hash(password));
            account.Unlock();
            LogAdminPasswordReset(logger, normalized);
        }

        Role adminRole = await dbContext.Roles
            .FirstAsync(r => r.EnterpriseId == null && r.Name == SystemRoles.Admin, cancellationToken)
            .ConfigureAwait(false);

        bool hasRole = await dbContext.UserParkRoles
            .AnyAsync(
                r => r.UserAccountId == account.Id && r.EnterpriseId == enterpriseId && r.WakeParkId == null
                    && r.RoleId == adminRole.Id && r.ValidToUtc == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (!hasRole)
        {
            dbContext.UserParkRoles.Add(UserParkRole.ForWholeEnterprise(account.Id, enterpriseId, adminRole.Id, clock.UtcNow));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return account.Id;
    }

    private static string DescribeRole(string roleName) => roleName switch
    {
        SystemRoles.Admin => "Plná správa systému",
        SystemRoles.Manager => "Správa areálu, provozu a rezervací",
        SystemRoles.Employee => "Obsluha na place: rezervace a odbavení",
        SystemRoles.Instructor => "Instruktor: náhled na rozvrh a rezervace",
        _ => roleName,
    };

    [LoggerMessage(EventId = 2100, Level = LogLevel.Information,
        Message = "Bootstrap správce není nakonfigurovaný, přeskakuji.")]
    private static partial void LogNoBootstrapAdmin(ILogger logger);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning,
        Message = "Založen bootstrap správce {Email}. Změň mu heslo hned po prvním přihlášení.")]
    private static partial void LogAdminCreated(ILogger logger, string email);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning,
        Message = "Správci {Email} nastaveno nové heslo a účet odemčen.")]
    private static partial void LogAdminPasswordReset(ILogger logger, string email);
}
