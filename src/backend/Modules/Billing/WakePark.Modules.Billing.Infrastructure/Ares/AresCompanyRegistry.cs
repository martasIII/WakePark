using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using WakePark.Modules.Billing.Application.Abstractions;

namespace WakePark.Modules.Billing.Infrastructure.Ares;

/// <summary>
/// Firmy z ARES (REST API Ministerstva financí, zdarma, limit 500 dotazů za minutu). Odpovědi
/// se drží v paměti — formulář se ptá při psaní IČO a objednávka se ptá znovu.
/// </summary>
internal sealed partial class AresCompanyRegistry(HttpClient http, IMemoryCache cache, ILogger<AresCompanyRegistry> logger)
    : ICompanyRegistry
{
    public static readonly Uri BaseAddress = new("https://ares.gov.cz/ekonomicke-subjekty-v-be/rest/");

    private static readonly TimeSpan FoundCacheTime = TimeSpan.FromHours(24);
    private static readonly TimeSpan NotFoundCacheTime = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RegisteredCompany?> FindAsync(string companyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyId);

        string key = $"ares:{companyId}";
        if (cache.TryGetValue(key, out RegisteredCompany? cached))
        {
            return cached;
        }

        RegisteredCompany? company = await LoadAsync(companyId, cancellationToken).ConfigureAwait(false);
        cache.Set(key, company, company is null ? NotFoundCacheTime : FoundCacheTime);
        return company;
    }

    private async Task<RegisteredCompany?> LoadAsync(string companyId, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http
                .GetAsync(new Uri($"ekonomicke-subjekty/{Uri.EscapeDataString(companyId)}", UriKind.Relative), cancellationToken)
                .ConfigureAwait(false);

            // 404 = subjekt neexistuje, 400 = ARES IČO odmítl jako neplatné. Obojí je „nenalezeno“.
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                LogUnexpectedStatus((int)response.StatusCode);
                throw new CompanyRegistryUnavailableException($"ARES odpověděl {(int)response.StatusCode}.");
            }

            AresSubject? subject = await response.Content
                .ReadFromJsonAsync<AresSubject>(Json, cancellationToken)
                .ConfigureAwait(false);

            return subject is { ObchodniJmeno.Length: > 0 }
                ? new RegisteredCompany(
                    subject.Ico ?? companyId,
                    subject.ObchodniJmeno.Trim(),
                    string.IsNullOrWhiteSpace(subject.Dic) ? null : subject.Dic.Trim(),
                    string.IsNullOrWhiteSpace(subject.Sidlo?.TextovaAdresa) ? null : subject.Sidlo.TextovaAdresa.Trim(),
                    subject.DatumZaniku)
                : throw new CompanyRegistryUnavailableException("ARES vrátil subjekt bez názvu.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogFailed(ex);
            throw new CompanyRegistryUnavailableException("ARES neodpovídá.", ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "ARES odpověděl neočekávaně: {Status}.")]
    private partial void LogUnexpectedStatus(int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dotaz do ARES selhal.")]
    private partial void LogFailed(Exception ex);

    // Jen pole, která potřebujeme. Názvy podle ARES (česky).
    private sealed record AresSubject(string? Ico, string? ObchodniJmeno, string? Dic, AresAddress? Sidlo, DateOnly? DatumZaniku);

    private sealed record AresAddress(string? TextovaAdresa);
}
