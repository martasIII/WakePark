using Microsoft.AspNetCore.Identity;

using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Domain.Users;

using AppPasswordVerification = WakePark.Modules.Identity.Application.Abstractions.PasswordVerification;

namespace WakePark.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Hashování hesel algoritmem z ASP.NET Core Identity (PBKDF2 s HMAC-SHA512).
/// </summary>
/// <remarks>
/// Vlastní implementace kryptografie je jedno z mála míst, kde se chyba nepozná až do průšvihu.
/// Bereme proto hotovou, prověřenou — jen ji schováváme za vlastní rozhraní, aby na ni
/// aplikační vrstva nebyla přivázaná.
/// </remarks>
internal sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        // Hasher instanci uživatele nepoužívá, jen ji vyžaduje podpis metody.
        return _hasher.HashPassword(null!, password);
    }

    public AppPasswordVerification Verify(string passwordHash, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        if (string.IsNullOrEmpty(password))
        {
            return AppPasswordVerification.Failed;
        }

        return _hasher.VerifyHashedPassword(null!, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => AppPasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => AppPasswordVerification.SuccessRehashNeeded,
            _ => AppPasswordVerification.Failed,
        };
    }
}
