using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using WakePark.Modules.Identity.Contracts;

namespace WakePark.Api.Infrastructure.Auth;

/// <summary>
/// Vyrábí autorizační politiku pro každé oprávnění za běhu.
/// </summary>
/// <remarks>
/// Bez toho by se musela každá politika registrovat ručně a na nové oprávnění by se
/// nevyhnutelně zapomnělo — s tím rozdílem, že chybějící politika nespadne při buildu,
/// ale až za běhu.
/// </remarks>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicies.Prefix, StringComparison.Ordinal))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[PermissionPolicies.Prefix.Length..]))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
