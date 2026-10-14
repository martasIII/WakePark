using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;

using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Options;

namespace WakePark.Storefront.Infrastructure;

/// <summary>Pomůcky pro šablony: aktuální jazyk, prefix v adrese, formátování cen a textů.</summary>
public sealed partial class SiteView(IOptions<StorefrontOptions> options)
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public StorefrontOptions Options { get; } = options.Value;

    public static CultureInfo Culture => CultureInfo.CurrentUICulture;

    public static string Language => Culture.TwoLetterISOLanguageName;

    public bool IsMultilingual => Options.SiteLanguages.Count > 1;

    /// <summary>Hodnota <c>lang</c> do odkazů: výchozí jazyk prefix nemá.</summary>
    public string? LanguageRoute => RouteFor(Language);

    public string? RouteFor(string language) =>
        string.Equals(language, Options.DefaultLanguage, StringComparison.Ordinal) ? null : language;

    /// <summary>Cena v korunách: „1 200 Kč“ (cs), „1,200 Kč“ (en). Mezery jsou nezlomitelné.</summary>
    public static string Price(decimal amount) => $"{amount.ToString("#,0", Culture)} Kč";

    /// <summary>Čistý text z obsahu; e-mailové adresy v něm se promění v odkazy.</summary>
    public static IHtmlContent Linkify(string text) =>
        new HtmlString(EmailPattern().Replace(Encoder.Encode(text), m => $"<a href=\"mailto:{m.Value}\">{m.Value}</a>"));

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex EmailPattern();
}
