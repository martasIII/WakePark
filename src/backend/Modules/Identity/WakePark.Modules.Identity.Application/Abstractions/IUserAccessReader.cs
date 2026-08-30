using WakePark.Modules.Identity.Contracts;

namespace WakePark.Modules.Identity.Application.Abstractions;

/// <summary>
/// Zjišťuje, co uživatel smí a kde.
/// </summary>
/// <remarks>
/// Oprávnění se čtou při každém požadavku, ne z cookie. Kdyby byla v cookie, odebrání role
/// by se projevilo až po odhlášení — a to je u zablokovaného zaměstnance pozdě.
/// </remarks>
public interface IUserAccessReader
{
    Task<CurrentUserDto?> GetAccessAsync(Guid userAccountId, CancellationToken cancellationToken = default);
}
