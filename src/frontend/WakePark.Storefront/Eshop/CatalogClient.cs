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
    LocalizedText? Description,
    string? ImagePath,
    decimal UnitPrice,
    string Currency,
    decimal VatRate);

/// <summary>Ceník na webu (<c>GET …/price-list</c>): sekce s podsekcemi, ceny platné dnes.</summary>
public sealed record WebPriceList(LocalizedText? Title, string Currency, IReadOnlyList<WebPriceSection> Sections);

public sealed record WebPriceSection(
    string Code,
    LocalizedText Name,
    LocalizedText? Description,
    IReadOnlyList<WebPriceItem> Items,
    IReadOnlyList<WebPriceSection> Subsections);

public sealed record WebPriceItem(string Code, LocalizedText Name, LocalizedText? Description, LocalizedText? Badge, decimal UnitPrice);

/// <summary>
/// Čte nabídku e-shopu a ceník z API (ADR 0009 — katalog spravuje backoffice). Úspěšnou
/// odpověď drží minutu v paměti: změna v backoffice se na webu projeví nejpozději za minutu
/// a návštěvník nečeká na API při každém načtení stránky.
/// </summary>
/// <remarks>
/// Chyba API není chyba webu: metoda vrátí <c>null</c> a stránka místo nabídky ukáže telefon.
/// Neúspěch se necachuje, další návštěvník to zkusí znovu. Ceník si navíc pamatuje poslední
/// úspěšnou odpověď a při výpadku API ukáže ji — jen se čte, nic se podle něj neprodává.
/// E-shop ne: prodávat podle staré nabídky by bylo horší než ukázat telefon.
/// </remarks>
public sealed partial class CatalogClient(
    HttpClient http,
    IOptions<StorefrontOptions> options,
    IMemoryCache cache,
    ILogger<CatalogClient> logger)
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    /// <summary>Jak dlouho smí ceník při výpadku API zůstat ze zálohy.</summary>
    public static readonly TimeSpan LastKnownDuration = TimeSpan.FromDays(1);

    private const string CacheKey = "eshop-catalog";
    private const string PriceListKey = "price-list";
    private const string LastKnownPriceListKey = "price-list:last-known";

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

    public async Task<WebPriceList?> GetPriceListAsync(CancellationToken cancellationToken = default)
    {
        StorefrontOptions settings = options.Value;

        if (!settings.IsEshopConfigured)
        {
            return null;
        }

        if (cache.TryGetValue(PriceListKey, out WebPriceList? cached))
        {
            return cached;
        }

        Uri url = new(settings.ApiBaseUrl!, $"/api/v1/catalog/enterprises/{settings.EnterpriseId}/price-list");

        try
        {
            WebPriceList? priceList = await http
                .GetFromJsonAsync<WebPriceList>(url, cancellationToken)
                .ConfigureAwait(false);

            if (priceList is not null)
            {
                cache.Set(PriceListKey, priceList, CacheDuration);
                cache.Set(LastKnownPriceListKey, priceList, LastKnownDuration);
            }

            return priceList;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogLoadFailed(url, ex);
            return cache.TryGetValue(LastKnownPriceListKey, out WebPriceList? lastKnown) ? lastKnown : null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Katalog se nepodařilo načíst z {Url}.")]
    private partial void LogLoadFailed(Uri url, Exception ex);
}
