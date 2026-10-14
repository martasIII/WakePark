namespace WakePark.Storefront;

/// <summary>Nastavení veřejného webu (sekce <c>Storefront</c> v appsettings).</summary>
public sealed class StorefrontOptions
{
    public const string SectionName = "Storefront";

    /// <summary>
    /// Jazyky webu. První je výchozí a žije v kořeni (<c>/cenik</c>), ostatní pod prefixem
    /// (<c>/en/cenik</c>). Jazyk přidej, až pro něj budou přeložené texty.
    /// </summary>
    /// <remarks>
    /// Bez výchozí hodnoty v kódu: binder konfigurace by položky z appsettings k výchozímu
    /// seznamu přidal, ne ho jimi nahradil. Používej <see cref="SiteLanguages"/>.
    /// </remarks>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>Složka s obsahem (JSON), relativně ke kořeni aplikace.</summary>
    public string ContentPath { get; init; } = "data";

    /// <summary>Časová zóna areálu — podle ní se pozná, který den je „dnes“.</summary>
    public string TimeZoneId { get; init; } = "Europe/Prague";

    /// <summary>Adresa rezervačního systému.</summary>
    public Uri ReservationUrl { get; init; } = new("https://r2.wakearealy.cz/cs/areal/8");

    /// <summary>
    /// Sítě reverzních proxy, kterým se věří hlavičky X-Forwarded-*. Na VPS běží web
    /// v Dockeru za nginxem hostitele, ten přichází z bridge sítě Dockeru.
    /// </summary>
    public IReadOnlyList<string> TrustedProxyNetworks { get; init; } = [];

    /// <summary>Jazyky bez duplicit; bez konfigurace jen čeština.</summary>
    public IReadOnlyList<string> SiteLanguages => Languages.Count == 0 ? ["cs"] : [.. Languages.Distinct(StringComparer.Ordinal)];

    public string DefaultLanguage => SiteLanguages[0];
}
