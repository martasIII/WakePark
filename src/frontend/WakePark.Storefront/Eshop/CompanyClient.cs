using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Options;

namespace WakePark.Storefront.Eshop;

// Vlastní kopie kontraktu API (Billing.Contracts) — web na moduly odkazovat nesmí.

/// <summary>Firma z registru ARES. DIČ jen u plátce DPH.</summary>
public sealed record CompanyInfo(string CompanyId, string Name, string? VatId, string? Address);

public enum CompanyLookupStatus
{
    Found,
    NotFound,
    Unavailable,
}

public sealed record CompanyLookup(CompanyLookupStatus Status, CompanyInfo? Company = null);

/// <summary>Firma podle IČO přes API (to se ptá ARES). Web sám do ARES nechodí.</summary>
public sealed partial class CompanyClient(HttpClient http, IOptions<StorefrontOptions> options, ILogger<CompanyClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CompanyLookup> FindAsync(string companyId, CancellationToken cancellationToken = default)
    {
        StorefrontOptions settings = options.Value;
        if (!settings.IsEshopConfigured)
        {
            return new CompanyLookup(CompanyLookupStatus.Unavailable);
        }

        Uri url = new(settings.ApiBaseUrl!, $"/api/v1/billing/companies/{Uri.EscapeDataString(companyId)}");

        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new CompanyLookup(CompanyLookupStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                LogUnexpectedStatus((int)response.StatusCode);
                return new CompanyLookup(CompanyLookupStatus.Unavailable);
            }

            CompanyInfo? company = await response.Content.ReadFromJsonAsync<CompanyInfo>(Json, cancellationToken).ConfigureAwait(false);
            return company is null
                ? new CompanyLookup(CompanyLookupStatus.Unavailable)
                : new CompanyLookup(CompanyLookupStatus.Found, company);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogFailed(ex);
            return new CompanyLookup(CompanyLookupStatus.Unavailable);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "API firem odpovědělo: {Status}.")]
    private partial void LogUnexpectedStatus(int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Volání API firem selhalo.")]
    private partial void LogFailed(Exception ex);
}
