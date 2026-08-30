using Microsoft.AspNetCore.Authorization;

namespace WakePark.Api.Infrastructure.Auth;

/// <summary>Požadavek na konkrétní oprávnění, například <c>reservations.cancel</c>.</summary>
internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
