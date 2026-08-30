using WakePark.Modules.Identity.Contracts;

namespace WakePark.Modules.Identity.Application.Authorization;

/// <summary>
/// Vyhodnocení, zda uživatel smí daný úkon.
/// </summary>
/// <remarks>
/// Čistá funkce nad tím, co vrátil <see cref="Abstractions.IUserAccessReader"/> — díky tomu
/// se pravidla dají otestovat bez databáze i bez HTTP.
/// </remarks>
public static class PermissionChecker
{
    /// <summary>
    /// Má uživatel oprávnění v daném areálu? Oprávnění platné pro celého provozovatele
    /// se počítá také.
    /// </summary>
    /// <param name="access">Přístupy uživatele.</param>
    /// <param name="permission">Kód oprávnění.</param>
    /// <param name="wakeParkId">Areál, kterého se úkon týká. Null = stačí oprávnění kdekoli.</param>
    public static bool Has(CurrentUserDto access, string permission, Guid? wakeParkId)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        if (access.GlobalPermissions.Contains(permission, StringComparer.Ordinal))
        {
            return true;
        }

        if (wakeParkId is null)
        {
            return access.Parks.Any(park => park.Permissions.Contains(permission, StringComparer.Ordinal));
        }

        return access.Parks
            .Where(park => park.WakeParkId == wakeParkId)
            .Any(park => park.Permissions.Contains(permission, StringComparer.Ordinal));
    }
}
