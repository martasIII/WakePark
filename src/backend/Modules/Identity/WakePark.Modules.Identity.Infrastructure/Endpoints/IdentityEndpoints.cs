using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Application.Authentication;
using WakePark.Modules.Identity.Application.Authorization;
using WakePark.Modules.Identity.Contracts;

namespace WakePark.Modules.Identity.Infrastructure.Endpoints;

/// <summary>HTTP rozhraní modulu Identity.</summary>
public static class IdentityEndpoints
{
    /// <summary>Typ claimu, ve kterém se veze otisk stavu účtu.</summary>
    public const string SecurityStampClaimType = "wakepark:security_stamp";

    /// <summary>Typ claimu s provozovatelem, pod kterého uživatel spadá.</summary>
    public const string EnterpriseClaimType = "wakepark:enterprise_id";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/identity")
            .WithTags("Identity");

        group.MapPost("/login", SignInAsync)
            .WithName("SignIn")
            .WithSummary("Přihlášení")
            .WithDescription("""
                Ověří e-mail a heslo a vystaví přihlašovací cookie.

                Cookie je `HttpOnly`, takže se k ní JavaScript nedostane — případný XSS
                nemůže session ukrást. Odpověď žádný token neobsahuje.

                Neúspěch vrací vždy **401** se stejnou hláškou bez ohledu na to, jestli účet
                neexistuje nebo je heslo špatné. Rozdílná odpověď by prozradila, které e-maily
                jsou v systému zavedené.

                Z jedné IP adresy jde nejvýš 10 pokusů za 10 minut, pak **429**. Po 5 špatných
                heslech za sebou se účet zamkne a odemkne ho až `create-admin`.
                """)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(IdentityModule.SignInRateLimitPolicy)
            .AllowAnonymous();

        // Přetypování na Delegate je nutné: metoda s jediným parametrem HttpContext by se
        // jinak vzala jako RequestDelegate a návratová hodnota by se zahodila.
        group.MapPost("/logout", (Delegate)SignOutAsync)
            .WithName("SignOut")
            .WithSummary("Odhlášení")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Kdo je přihlášený a co smí")
            .WithDescription(
                "Oprávnění se čtou z databáze při každém volání, ne z cookie — odebrání role "
                + "se tak projeví okamžitě, ne až po odhlášení.")
            .Produces<CurrentUserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> SignInAsync(
        SignInRequest request,
        ICommandHandler<SignInCommand, SignedInUser> handler,
        IUserAccessReader accessReader,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        SignedInUser user = await handler
            .HandleAsync(new SignInCommand(request.Email, request.Password), cancellationToken)
            .ConfigureAwait(false);

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, user.UserAccountId.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(SecurityStampClaimType, user.SecurityStamp),
        ];

        // Provozovatel se do cookie ukládá záměrně: potřebují ho filtry dat, které běží
        // synchronně, a příslušnost k firmě se prakticky nemění. Role naopak v cookie nejsou,
        // ty se čtou při každém požadavku.
        CurrentUserDto? access = await accessReader
            .GetAccessAsync(user.UserAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (access?.EnterpriseId is { } enterpriseId)
        {
            claims.Add(new Claim(EnterpriseClaimType, enterpriseId.ToString()));
        }

        ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await httpContext
            .SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity))
            .ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> SignOutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        IQueryHandler<GetCurrentUserQuery, CurrentUserDto> handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string? id = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(id, out Guid userAccountId))
        {
            return Results.Unauthorized();
        }

        CurrentUserDto user = await handler
            .HandleAsync(new GetCurrentUserQuery(userAccountId), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(user);
    }
}

/// <summary>Tělo požadavku na přihlášení.</summary>
public sealed record SignInRequest(string Email, string Password);
