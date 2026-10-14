using System.Text.Json;
using System.Text.Json.Serialization;

namespace WakePark.Storefront.Content;

/// <summary>
/// Text v obsahu webu. V JSONu je buď prostý řetězec (platí pro všechny jazyky,
/// např. <c>"10:00–20:00"</c>), nebo objekt <c>{"cs": "…", "en": "…"}</c>.
/// Chybí-li překlad, použije se čeština — stránka se kvůli chybějícímu překladu nerozbije.
/// </summary>
[JsonConverter(typeof(LocalizedTextJsonConverter))]
public sealed class LocalizedText
{
    public const string FallbackLanguage = "cs";

    private readonly string? _invariant;
    private readonly IReadOnlyDictionary<string, string> _translations;

    private LocalizedText(string? invariant, IReadOnlyDictionary<string, string> translations)
    {
        _invariant = invariant;
        _translations = translations;
    }

    public static LocalizedText Invariant(string text) => new(text, new Dictionary<string, string>());

    public static LocalizedText Of(IReadOnlyDictionary<string, string> translations) => new(null, translations);

    /// <summary>Jazyky, pro které má text vlastní překlad. Prostý řetězec nemá žádný.</summary>
    public IEnumerable<string> Languages => _translations.Keys;

    public bool IsInvariant => _invariant is not null;

    public string Resolve(string language)
    {
        if (_invariant is not null)
        {
            return _invariant;
        }

        if (_translations.TryGetValue(language, out string? text) ||
            _translations.TryGetValue(FallbackLanguage, out text))
        {
            return text;
        }

        return _translations.Values.FirstOrDefault() ?? string.Empty;
    }

    public override string ToString() => Resolve(FallbackLanguage);
}

/// <summary>Čte <see cref="LocalizedText"/> z řetězce i z objektu s překlady.</summary>
public sealed class LocalizedTextJsonConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => LocalizedText.Invariant(reader.GetString()!),
            JsonTokenType.StartObject => LocalizedText.Of(
                JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options)
                ?? throw new JsonException("Prázdný objekt s překlady.")),
            _ => throw new JsonException(
                $"Text musí být řetězec nebo objekt {{\"cs\": \"…\"}}, ne {reader.TokenType}."),
        };
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        if (value.IsInvariant)
        {
            writer.WriteStringValue(value.ToString());
            return;
        }

        writer.WriteStartObject();
        foreach (string language in value.Languages)
        {
            writer.WriteString(language, value.Resolve(language));
        }

        writer.WriteEndObject();
    }
}
