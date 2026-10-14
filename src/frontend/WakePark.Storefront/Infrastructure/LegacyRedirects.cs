using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Rewrite;

namespace WakePark.Storefront.Infrastructure;

/// <summary>
/// Přesměrování adres ze starého WordPressu a z dočasného statického webu, ať odkazy
/// a pozice ve vyhledávačích nepřijdou nazmar. Trvalé (301), kromě rezervací — ty vedou
/// na cizí systém, jehož adresa se může změnit.
/// </summary>
public static partial class LegacyRedirects
{
    public static RewriteOptions Create(Uri reservationUrl)
    {
        ArgumentNullException.ThrowIfNull(reservationUrl);

        return new RewriteOptions()
            // dočasný statický web běžel ve složce /novy/
            .AddRedirect("^novy/?(.*)$", "$1", StatusCodes.Status301MovedPermanently)
            .AddRedirect("^index\\.html?$", "/", StatusCodes.Status301MovedPermanently)
            .AddRedirect("^(cenik|kontakt)(\\.html|/)$", "$1", StatusCodes.Status301MovedPermanently)
            // e-shop WordPressu (WooCommerce) → vouchery v ceníku
            .Add(RedirectShopToVouchers)
            .AddRedirect("^rezervace/?$", reservationUrl.ToString(), StatusCodes.Status302Found);
    }

    /// <summary>
    /// Vlastní pravidlo místo <c>AddRedirect</c>: to by v cíli zakódovalo <c>#</c> na <c>%23</c>
    /// a prohlížeč by místo skoku na sekci hledal stránku „cenik#vouchery“.
    /// </summary>
    private static void RedirectShopToVouchers(RewriteContext context)
    {
        string path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (!ShopPattern().IsMatch(path))
        {
            return;
        }

        HttpResponse response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status301MovedPermanently;
        response.Headers.Location = "/cenik#vouchery";
        context.Result = RuleResult.EndResponse;
    }

    [GeneratedRegex("^/(shop|obchod|kosik|pokladna|produkt|vouchery?)(/.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex ShopPattern();
}
