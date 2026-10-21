namespace WakePark.Modules.Billing.Infrastructure.Fio;

/// <summary>Párování plateb přes API Fio banky (sekce <c>Billing:Fio</c>).</summary>
public sealed class FioOptions
{
    public const string SectionName = "Billing:Fio";

    /// <summary>
    /// Token jen pro čtení („Sledovat účet“). Nikdy v repu ani v logu — na serveru v <c>.env</c>.
    /// Bez tokenu párování neběží.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>Provozovatel, kterému účet patří.</summary>
    public Guid? EnterpriseId { get; init; }

    public Uri BaseUrl { get; init; } = new("https://fioapi.fio.cz");

    /// <summary>
    /// Kolik dní zpětně se výpis stahuje pokaždé znovu. Neposouvá se zarážka v bance, takže
    /// pád aplikace mezi stažením a uložením nic neztratí; duplicity odfiltruje inbox.
    /// </summary>
    public int LookbackDays { get; init; } = 7;

    /// <summary>Jak často se párují platby. Fio dovoluje nejvýš jeden dotaz za 30 s.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);

    public string TimeZoneId { get; init; } = "Europe/Prague";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Token) && EnterpriseId is not null;
}
