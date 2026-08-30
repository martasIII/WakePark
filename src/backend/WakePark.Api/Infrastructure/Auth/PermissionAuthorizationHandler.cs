using Microsoft.AspNetCore.Authorization;

using WakePark.Modules.Identity.Application.Authorization;
using WakePark.Modules.Identity.Contracts;
using WakePark.Modules.Operations.Contracts;

namespace WakePark.Api.Infrastructure.Auth;

/// <summary>
/// Ověří, že uživatel má požadované oprávnění <b>v areálu, kterého se požadavek týká</b>.
/// </summary>
/// <remarks>
/// Areál se hledá v parametrech požadavku. Přímo zadaný <c>wakeParkId</c> nebo <c>parkId</c>
/// se použije rovnou; když je zadaný jen <c>cableId</c>, dohledá se přes kontrakt modulu
/// Operations, ke kterému areálu vlek patří. Bez toho by zaměstnanec jednoho parku mohl
/// zasahovat do provozu druhého.
///
/// Když v požadavku areál není vůbec (typicky výpisy), stačí oprávnění v kterémkoli areálu
/// — samotný výpis si pak musí data omezit sám.
/// </remarks>
internal sealed class PermissionAuthorizationHandler(
    CurrentUserAccess currentUserAccess,
    ICableDirectory cableDirectory,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        CurrentUserDto? access = await currentUserAccess.GetAsync().ConfigureAwait(false);

        if (access is null)
        {
            return;
        }

        Guid? wakeParkId = await ResolveParkAsync().ConfigureAwait(false);

        if (PermissionChecker.Has(access, requirement.Permission, wakeParkId))
        {
            context.Succeed(requirement);
        }
    }

    private async Task<Guid?> ResolveParkAsync()
    {
        HttpContext? http = httpContextAccessor.HttpContext;

        if (http is null)
        {
            return null;
        }

        if (TryReadGuid(http, "wakeParkId", out Guid parkId) || TryReadGuid(http, "parkId", out parkId))
        {
            return parkId;
        }

        if (TryReadGuid(http, "cableId", out Guid cableId))
        {
            CableDto? cable = await cableDirectory
                .GetCableAsync(cableId, http.RequestAborted)
                .ConfigureAwait(false);

            return cable?.WakeParkId;
        }

        return null;
    }

    private static bool TryReadGuid(HttpContext http, string name, out Guid value)
    {
        if (http.Request.RouteValues.TryGetValue(name, out object? routeValue)
            && Guid.TryParse(routeValue?.ToString(), out value))
        {
            return true;
        }

        if (http.Request.Query.TryGetValue(name, out Microsoft.Extensions.Primitives.StringValues queryValue)
            && Guid.TryParse(queryValue.ToString(), out value))
        {
            return true;
        }

        value = Guid.Empty;
        return false;
    }
}
