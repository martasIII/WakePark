namespace WakePark.Modules.Notifications.Application;

/// <summary>Nastavení e-mailů (sekce <c>Notifications</c>).</summary>
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Odesílání zapnuté? Vypnuté e-maily se dál připravují do fronty, jen se neposílají —
    /// a starší než <see cref="MaxAge"/> už se neodešlou nikdy.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>Adresa webu pro odkazy v e-mailech, např. <c>https://novy.wake-dzban.cz</c>.</summary>
    public Uri PublicBaseUrl { get; init; } = new("https://novy.wake-dzban.cz");

    public string FromAddress { get; init; } = "noreply@wakeparksro.cz";

    public string FromName { get; init; } = "Wake Park Džbán";

    /// <summary>Kam jdou odpovědi zákazníků — schránka, kterou někdo čte.</summary>
    public string ReplyTo { get; init; } = "info@wake-dzban.cz";

    public string Phone { get; init; } = "+420 737 562 516";

    /// <summary>Kam chodí upozornění pro provozovatele (platby k ručnímu vyřízení, chyby).</summary>
    public string AdminEmail { get; init; } = "info@wake-dzban.cz";

    /// <summary>E-mail, který se do té doby neodešle, propadne.</summary>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromHours(24);

    public SmtpOptions Smtp { get; init; } = new();

    /// <summary>Barva pruhu na PDF poukazu podle kódu produktu (1/2/3 na první pohled).</summary>
    public IReadOnlyDictionary<string, string> VoucherColors { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["voucher-1-slot"] = "#e50051",
        ["voucher-2-sloty"] = "#0077c8",
        ["voucher-3-sloty"] = "#00a651",
        ["permanentka-10x20"] = "#222222",
    };
}

public sealed class SmtpOptions
{
    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 587;

    /// <summary>Šifrování přes STARTTLS (port 587). Mailpit při vývoji šifrování nemá.</summary>
    public bool UseStartTls { get; init; } = true;

    public string? Username { get; init; }

    /// <summary>Heslo jen z prostředí serveru (.env), nikdy z repa.</summary>
    public string? Password { get; init; }
}
