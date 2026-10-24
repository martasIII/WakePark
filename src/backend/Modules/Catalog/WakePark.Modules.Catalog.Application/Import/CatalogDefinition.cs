using System.Text.Json;
using System.Text.Json.Serialization;

namespace WakePark.Modules.Catalog.Application.Import;

/// <summary>
/// Obsah souboru <c>katalog.json</c> — výchozí katalog. Od backoffice (ADR 0009) je zdrojem
/// pravdy databáze: import při nasazení jen doplní, co v ní chybí, a nic nepřepíše.
/// </summary>
/// <remarks>Pořadí kategorií a produktů v souboru je pořadí na webu.</remarks>
public sealed record CatalogDefinition(
    string Currency,
    IReadOnlyDictionary<string, string>? Title,
    IReadOnlyList<CategoryDefinition> Categories,
    IReadOnlyList<ProductDefinition> Products)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static async Task<CatalogDefinition> ReadAsync(Stream json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);

        return await JsonSerializer
                .DeserializeAsync<CatalogDefinition>(json, JsonOptions, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new JsonException("Soubor katalogu je prázdný.");
    }
}

/// <summary>Kategorie. <see cref="Parent"/> = kód nadřazené kategorie (podkategorie).</summary>
public sealed record CategoryDefinition(
    string Code,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description = null,
    string? Parent = null);

/// <summary>
/// Produkt v souboru. Cena je včetně DPH, sazba DPH v procentech. <see cref="ShowIn"/> = kde
/// se ukazuje: <c>pricelist</c> (ceník na webu), <c>eshop</c>, <c>pos</c> (pokladna).
/// </summary>
public sealed record ProductDefinition(
    string Code,
    string Category,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    string? Image,
    decimal Price,
    decimal VatRate,
    IReadOnlyList<string> ShowIn,
    IReadOnlyDictionary<string, string>? Badge = null);
