using System.Text.Json;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace WakePark.Storefront.Content;

// Vlastní kopie kontraktu API (Operations.Contracts) — web na moduly odkazovat nesmí.

/// <summary>Otevírací doba z API: dny od dneška dopředu. Den bez časů = zavřeno.</summary>
public sealed record WebOpeningHours(string TimeZoneId, IReadOnlyList<WebOpeningDay> Days);

public sealed record WebOpeningDay(DateOnly Date, TimeOnly? OpensAt, TimeOnly? ClosesAt, IReadOnlyList<WebBlocking> Blockings);

/// <summary>Blokace v rámci otevírací doby. Bez vleku = celý areál. Kategorie: TechnicalFault, PrivateEvent, Weather, Other.</summary>
public sealed record WebBlocking(string? CableName, TimeOnly From, TimeOnly To, string Category);

/// <summary>
/// Otevírací doba z API (ADR 0009). Úspěšnou odpověď drží minutu; při výpadku API ukáže
/// poslední úspěšnou — jen se čte. Bez úspěšné odpovědi vrátí null a stránka ukáže telefon.
/// </summary>
public sealed partial class OpeningHoursClient(
    HttpClient http,
    IOptions<StorefrontOptions> options,
    IMemoryCache cache,
    ILogger<OpeningHoursClient> logger)
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan LastKnownDuration = TimeSpan.FromDays(1);

    private const string CacheKey = "opening-hours";
    private const string LastKnownKey = "opening-hours:last-known";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<WebOpeningHours?> GetAsync(CancellationToken cancellationToken = default)
    {
        StorefrontOptions settings = options.Value;

        if (!settings.IsOpeningHoursApiConfigured)
        {
            return null;
        }

        if (cache.TryGetValue(CacheKey, out WebOpeningHours? cached))
        {
            return cached;
        }

        Uri url = new(settings.ApiBaseUrl!, $"/api/v1/operations/parks/{settings.WakeParkId}/opening-hours");

        try
        {
            WebOpeningHours? hours = await http.GetFromJsonAsync<WebOpeningHours>(url, Json, cancellationToken).ConfigureAwait(false);

            if (hours is not null)
            {
                cache.Set(CacheKey, hours, CacheDuration);
                cache.Set(LastKnownKey, hours, LastKnownDuration);
            }

            return hours;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogLoadFailed(url, ex);
            return cache.TryGetValue(LastKnownKey, out WebOpeningHours? lastKnown) ? lastKnown : null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Otevírací dobu se nepodařilo načíst z {Url}.")]
    private partial void LogLoadFailed(Uri url, Exception ex);
}
