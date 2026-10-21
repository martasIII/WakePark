using System.Text.Json;
using System.Text.Json.Serialization;

namespace WakePark.Modules.Catalog.Application.Import;

/// <summary>
/// Obsah souboru <c>katalog.json</c>. Dokud není backoffice, je tohle jediný způsob, jak měnit
/// nabídku e-shopu: úprava souboru → commit → nasazení → import (ADR 0006).
/// </summary>
/// <remarks>Pořadí kategorií a produktů v souboru je pořadí na webu.</remarks>
public sealed record CatalogDefinition(
    string Currency,
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

public sealed record CategoryDefinition(
    string Code,
    IReadOnlyDictionary<string, string> Name);

/// <summary>Produkt v souboru. Cena je včetně DPH, sazba DPH v procentech.</summary>
public sealed record ProductDefinition(
    string Code,
    string Category,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description,
    string? Image,
    decimal Price,
    decimal VatRate);
