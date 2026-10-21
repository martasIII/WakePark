using System.Text.Json;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Vícejazyčný text se ukládá jako <c>jsonb</c> ve tvaru <c>{"cs": "…", "en": "…"}</c> — stejně
/// jako v souborech obsahu webu. Sloupec na každý jazyk by s každým novým jazykem znamenal migraci.
/// </summary>
internal sealed class LocalizedTextConverter() : ValueConverter<LocalizedText, string>(
    text => ToJson(text),
    json => FromJson(json))
{
    public const string ColumnType = "jsonb";

    private static string ToJson(LocalizedText text) => JsonSerializer.Serialize(text.Translations);

    private static LocalizedText FromJson(string json) =>
        LocalizedText.Create(
            JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new JsonException("Prázdný vícejazyčný text v databázi."));
}
