using WakePark.Modules.Identity.Domain.Users;

namespace WakePark.Modules.Identity.Application.Abstractions;

/// <summary>Přístup k přihlašovacím účtům.</summary>
public interface IUserAccountRepository
{
    /// <summary>Načte účet pro zápis. E-mail se dohledává v normalizované podobě.</summary>
    Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetByIdAsync(Guid userAccountId, CancellationToken cancellationToken = default);

    void Add(UserAccount account);
}
