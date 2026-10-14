using System.Text.Json;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace WakePark.Storefront.Content;

/// <summary>
/// Obsah webu z JSON souborů. Soubor se načte při prvním použití a drží v paměti, dokud
/// se na disku nezmění — úprava ceníku se tak projeví bez restartu.
/// </summary>
public sealed class SiteContent : IDisposable
{
    public const string PriceListFile = "cenik.json";
    public const string OpeningHoursFile = "oteviraci-doba.json";

    private readonly IMemoryCache _cache;
    private readonly PhysicalFileProvider _files;

    public SiteContent(IOptions<StorefrontOptions> options, IWebHostEnvironment environment, IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        _cache = cache;
        _files = new PhysicalFileProvider(Path.Combine(environment.ContentRootPath, options.Value.ContentPath));
    }

    public PriceList PriceList => Load<PriceList>(PriceListFile);

    public OpeningHours OpeningHours => Load<OpeningHours>(OpeningHoursFile);

    public void Dispose() => _files.Dispose();

    private T Load<T>(string fileName) where T : class, IValidatedContent =>
        _cache.GetOrCreate($"content:{fileName}", entry =>
        {
            entry.AddExpirationToken(_files.Watch(fileName));

            IFileInfo file = _files.GetFileInfo(fileName);
            if (!file.Exists)
            {
                throw new FileNotFoundException($"Chybí soubor s obsahem webu: {file.PhysicalPath ?? fileName}");
            }

            using Stream stream = file.CreateReadStream();
            return ContentJson.ReadValid<T>(stream, fileName);
        })!;
}

/// <summary>Obsah, který umí zkontrolovat sám sebe (chybějící cena, den v týdnu …).</summary>
public interface IValidatedContent
{
    /// <summary>Popisy chyb česky, pro člověka, který JSON upravuje. Prázdné = v pořádku.</summary>
    IReadOnlyList<string> Validate();
}

/// <summary>Jednotné čtení obsahových JSONů — používá web i testy.</summary>
public static class ContentJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Načte a zkontroluje obsah. Web používá jen tuhle variantu: JSON, který je syntakticky
    /// v pořádku, ale nedává smysl (položka bez ceny), se tak nedostane do šablony.
    /// </summary>
    public static T ReadValid<T>(Stream stream, string fileName) where T : class, IValidatedContent
    {
        T content = Read<T>(stream, fileName);

        IReadOnlyList<string> errors = content.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidDataException($"{fileName}: {string.Join(" ", errors)}");
        }

        return content;
    }

    /// <summary>Jen načte, bez kontroly obsahu — pro testy samotné validace.</summary>
    public static T Read<T>(Stream stream, string fileName) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(stream, Options)
                ?? throw new InvalidDataException($"{fileName}: soubor je prázdný.");
        }
        catch (JsonException ex)
        {
            // Řádek a pozice chyby, ať se dá rozbitý JSON rychle najít.
            throw new InvalidDataException(
                $"{fileName}: chyba na řádku {ex.LineNumber + 1}, pozice {ex.BytePositionInLine + 1}: {ex.Message}", ex);
        }
    }
}
