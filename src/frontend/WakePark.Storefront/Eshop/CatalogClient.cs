using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using WakePark.Storefront.Content;

namespace WakePark.Storefront.Eshop;

/// <summary>
/// Nabídka e-shopu tak, jak ji vrací API (<c>GET /api/v1/catalog/enterprises/{id}</c>).
/// Vlastní kopie kontraktu — web na moduly backendu odkazovat nesmí (StorefrontIsolationTests).
/// </summary>
public sealed record EshopCatalog(IReadOnlyList<EshopCategory> Categories, IReadOnlyList<EshopProduct> Products);

public sealed record EshopCategory(string Code, LocalizedText Name);

public sealed record EshopProduct(
    Guid Id,
    string Code,
    string CategoryCode,
    LocalizedText Name,
    LocalizedText Description,
    string? ImagePath,
    decimal UnitPrice,
    string Currency,
    decimal VatRate);

/// <summary>
/// Čte nabídku z API. Úspěšnou odpověď drží minutu v paměti — nabídka se mění jen
/// nasazením, a návštěvník tak nečeká na API při každém načtení stránky.
/// </summary>
/// <remarks>
/// Chyba API není chyba webu: metoda vrátí <c>null</c> a stránka místo nabídky ukáže telefon.
/// Neúspěch se necachuje, další návštěvník to zkusí znovu.
/// </remarks>
public sealed partial class CatalogClient(
    HttpClient http,
    IOptions<StorefrontOptions> options,
    IMemoryCache cache,
    ILogger<CatalogClient> logger)
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    private const string CacheKey = "eshop-catalog";

    public async Task<EshopCatalog?> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        StorefrontOptions settings = options.Value;

        if (!settings.IsEshopConfigured)
        {
            return null;
        }

        if (cache.TryGetValue(CacheKey, out EshopCatalog? cached))
        {
            return cached;
        }

        Uri url = new(settings.ApiBaseUrl!, $"/api/v1/catalog/enterprises/{settings.EnterpriseId}");

        try
        {
            EshopCatalog? catalog = await http
                .GetFromJsonAsync<EshopCatalog>(url, cancellationToken)
                .ConfigureAwait(false);

            if (catalog is not null)
            {
                cache.Set(CacheKey, catalog, CacheDuration);
            }

            return catalog;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogLoadFailed(url, ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Nabídku e-shopu se nepodařilo načíst z {Url}.")]
    private partial void LogLoadFailed(Uri url, Exception ex);
}
