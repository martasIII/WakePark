namespace WakePark.Storefront.Infrastructure;

/// <summary>
/// Bezpečnostní hlavičky pro každou odpověď. Web je po napadení WordPressu (07/2026)
/// záměrně utažený: žádné skripty, žádné vkládání do cizích stránek.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Zdroje, ze kterých smí stránka cokoli načíst. Font Montserrat se zatím bere
    /// z Google Fonts; až bude na vlastním serveru, obě Google domény odsud zmizí.
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' https://fonts.googleapis.com; " +
        "font-src https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public static Task Apply(HttpContext context, Func<Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        IHeaderDictionary headers = context.Response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        return next();
    }
}
