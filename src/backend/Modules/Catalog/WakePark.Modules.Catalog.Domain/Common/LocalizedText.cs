using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.ValueObjects;

namespace WakePark.Modules.Catalog.Domain.Common;

/// <summary>
/// Text v několika jazycích, klíčem je dvoupísmenný kód jazyka (<c>cs</c>, <c>en</c> …).
/// Čeština je povinná — je to jazyk, do kterého se zobrazení vrací, když překlad chybí.
/// </summary>
public sealed class LocalizedText : ValueObject
{
    public const string RequiredLanguage = "cs";

    private readonly SortedDictionary<string, string> _translations;

    private LocalizedText(SortedDictionary<string, string> translations)
    {
        _translations = translations;
    }

    public IReadOnlyDictionary<string, string> Translations => _translations;

    public static LocalizedText Create(IReadOnlyDictionary<string, string> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        SortedDictionary<string, string> cleaned = new(StringComparer.Ordinal);

        foreach ((string language, string text) in translations)
        {
            string code = language.Trim().ToLowerInvariant();

            if (code.Length != 2 || !code.All(char.IsAsciiLetterLower))
            {
                throw new DomainException(
                    "localized_text.invalid_language",
                    $"Kód jazyka musí mít dvě písmena (cs, en …), ne '{language}'.");
            }

            // Prázdný překlad je totéž co chybějící — zobrazí se čeština.
            if (!string.IsNullOrWhiteSpace(text))
            {
                cleaned[code] = text.Trim();
            }
        }

        if (!cleaned.ContainsKey(RequiredLanguage))
        {
            throw new DomainException("localized_text.czech_required", "Text musí mít českou verzi.");
        }

        return new LocalizedText(cleaned);
    }

    public static LocalizedText Czech(string text) =>
        Create(new Dictionary<string, string> { [RequiredLanguage] = text });

    public override string ToString() => _translations[RequiredLanguage];

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach ((string language, string text) in _translations)
        {
            yield return language;
            yield return text;
        }
    }
}
