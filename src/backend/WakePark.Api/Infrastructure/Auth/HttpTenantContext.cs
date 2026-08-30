using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Identity.Infrastructure.Endpoints;

namespace WakePark.Api.Infrastructure.Auth;

/// <summary>
/// Provozovatel, pod kterého spadá přihlášený uživatel.
/// </summary>
/// <remarks>
/// Čte se z claimu vystaveného při přihlášení, ne z databáze. Rozhraní je synchronní, protože
/// ho budou volat globální filtry EF — a sahat v nich do databáze by znamenalo dotaz uvnitř
/// dotazu. Příslušnost k provozovateli se navíc prakticky nemění, takže hodnota v cookie
/// nezastarává tak, jako by zastaraly role.
///
/// Do zavedení autentizace bylo <see cref="ITenantContext"/> jen deklarované rozhraní
/// bez jediné implementace.
/// </remarks>
internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? EnterpriseId
    {
        get
        {
            string? value = httpContextAccessor.HttpContext?.User
                .FindFirst(IdentityEndpoints.EnterpriseClaimType)?.Value;

            return Guid.TryParse(value, out Guid enterpriseId) ? enterpriseId : null;
        }
    }
}
