using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Domain.Users;
using WakePark.Modules.Identity.Infrastructure.Endpoints;

namespace WakePark.Api.Infrastructure.Auth;

/// <summary>
/// Při každém požadavku ověří, že cookie odpovídá aktuálnímu stavu účtu.
/// </summary>
/// <remarks>
/// Cookie sama o sobě platí, dokud nevyprší. Kdyby se nic nekontrolovalo, zablokovaný
/// zaměstnanec nebo změněné heslo by se projevily až za osm hodin. Otisk uložený v cookie
/// se proto porovnává s otiskem v databázi — při každé změně hesla nebo zablokování se
/// otisk mění a existující přihlášení tím okamžitě padá.
///
/// Stojí to jeden dotaz na požadavek. Až to začne vadit, dá se výsledek krátce kešovat;
/// vyměňuje se tím rychlost za zpoždění, se kterým se blokace projeví.
/// </remarks>
internal static class SessionSecurityStampValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? stamp = context.Principal?.FindFirstValue(IdentityEndpoints.SecurityStampClaimType);

        if (!Guid.TryParse(id, out Guid userAccountId) || string.IsNullOrEmpty(stamp))
        {
            await RejectAsync(context).ConfigureAwait(false);
            return;
        }

        IUserAccountRepository repository = context.HttpContext.RequestServices
            .GetRequiredService<IUserAccountRepository>();

        UserAccount? account = await repository
            .GetByIdAsync(userAccountId, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (account is null || !account.CanSignIn
            || !string.Equals(account.SecurityStamp, stamp, StringComparison.Ordinal))
        {
            await RejectAsync(context).ConfigureAwait(false);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();

        await context.HttpContext
            .SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            .ConfigureAwait(false);
    }
}
