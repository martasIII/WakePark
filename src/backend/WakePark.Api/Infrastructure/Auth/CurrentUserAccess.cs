using System.Security.Claims;

using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Contracts;

namespace WakePark.Api.Infrastructure.Auth;

/// <summary>
/// Přístupy přihlášeného uživatele načtené jednou za požadavek.
/// </summary>
/// <remarks>
/// Oprávnění se čtou z databáze, ne z cookie — jinak by se odebrání role projevilo
/// až po odhlášení. Aby to nestálo dotaz za každou kontrolu, drží se výsledek po dobu
/// jednoho požadavku.
/// </remarks>
internal sealed class CurrentUserAccess(IUserAccessReader reader, IHttpContextAccessor httpContextAccessor)
{
    private CurrentUserDto? _cached;
    private bool _loaded;

    public async Task<CurrentUserDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return _cached;
        }

        _loaded = true;

        string? id = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(id, out Guid userAccountId))
        {
            _cached = await reader.GetAccessAsync(userAccountId, cancellationToken).ConfigureAwait(false);
        }

        return _cached;
    }
}
