using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace WakePark.Api.Infrastructure.Auth;

/// <summary>
/// Ochrana proti CSRF metodou dvojitého odeslání tokenu.
/// </summary>
/// <remarks>
/// Přihlášení drží cookie, kterou prohlížeč přiloží i k požadavku vyvolanému cizí stránkou.
/// Samotné <c>SameSite=Strict</c> je dobrá obrana, ale spoléhat se jen na ni znamená
/// spoléhat na chování prohlížeče.
///
/// ASP.NET Core pracuje se dvěma tokeny: jeden si drží v <b>vlastní HttpOnly cookie</b>,
/// druhý — request token — musí klient poslat zpět. Proto se při každém bezpečném požadavku
/// vypíše request token do <b>čitelné</b> cookie <c>XSRF-TOKEN</c> a u požadavků měnících stav
/// se vyžaduje v hlavičce <c>X-XSRF-TOKEN</c>.
///
/// Cizí stránka obě cookie k požadavku přiloží, ale přečíst je a zkopírovat do hlavičky nemůže —
/// brání jí v tom pravidlo stejného původu. Angular tenhle postup podporuje zabudovaně
/// a hlavičku doplňuje sám.
///
/// Endpoint, který přihlášení nepoužívá (objednávka z webu, volaná serverem webu), se může
/// odhlásit standardním <c>.DisableAntiforgery()</c>. Výjimka je výslovná u endpointu, ne
/// odvozená z požadavku — jinak by se dala zneužít i tam, kde ochrana být má.
/// </remarks>
internal sealed class AntiforgeryMiddleware(RequestDelegate next)
{
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(antiforgery);

        if (context.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>() is { RequiresValidation: false })
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (SafeMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            IssueRequestToken(context, antiforgery);
            await next(context).ConfigureAwait(false);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            await WriteProblemAsync(context).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Uloží request token do cookie, kterou smí přečíst JavaScript. HttpOnly cookie
    /// s druhým tokenem si mezitím obhospodařuje samo ASP.NET Core.
    /// </summary>
    private static void IssueRequestToken(HttpContext context, IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);

        if (tokens.RequestToken is null)
        {
            return;
        }

        context.Response.Cookies.Append(
            "XSRF-TOKEN",
            tokens.RequestToken,
            new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Strict,
                Secure = context.Request.IsHttps,
                Path = "/",
            });
    }

    private static async Task WriteProblemAsync(HttpContext context)
    {
        ProblemDetails problem = new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Chybí ochranný token",
            Detail = "Požadavek neobsahoval platnou hlavičku X-XSRF-TOKEN.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted).ConfigureAwait(false);
    }
}
