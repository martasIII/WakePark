using System.Security.Claims;

using WakePark.BuildingBlocks.Application.Abstractions;

namespace WakePark.Api.Infrastructure;

/// <summary>
/// Aktuální uživatel odvozený z HTTP kontextu. Dokud není hotový modul Identity,
/// vrací neautentizovaného uživatele — audit pak zapisuje null, což je korektní.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserAccountId =>
        Guid.TryParse(FindClaim(ClaimTypes.NameIdentifier), out Guid id) ? id : null;

    public string? DisplayName => FindClaim(ClaimTypes.Name);

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    private string? FindClaim(string claimType) =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
}
