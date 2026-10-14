using System.Text.RegularExpressions;

namespace WakePark.Storefront.Content;

/// <summary>Ceník placený na místě (<c>data/cenik.json</c>). Není to katalog e-shopu.</summary>
public sealed partial record PriceList(LocalizedText? Season, IReadOnlyList<PriceSection> Sections) : IValidatedContent
{
    /// <summary>
    /// Kontrola obsahu. Spouští se v testech, takže rozbitý ceník neprojde CI
    /// a na web se nedostane.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (Sections is null || Sections.Count == 0)
        {
            errors.Add("Ceník nemá žádnou sekci.");
            return errors;
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int s = 0; s < Sections.Count; s++)
        {
            PriceSection section = Sections[s];
            string where = $"sekce {s + 1}";

            if (IsBlank(section.Name))
            {
                errors.Add($"{where}: chybí název (\"name\").");
            }
            else
            {
                where = $"sekce „{section.Name}“";
            }

            if (section.Id is not null && !SlugPattern().IsMatch(section.Id))
            {
                errors.Add($"{where}: id „{section.Id}“ smí obsahovat jen malá písmena bez diakritiky, číslice a pomlčky.");
            }

            if (section.Id is not null && !ids.Add(section.Id))
            {
                errors.Add($"{where}: id „{section.Id}“ už má jiná sekce.");
            }

            if ((section.Items is null || section.Items.Count == 0) && section.Note is null)
            {
                errors.Add($"{where}: nemá položky ani poznámku — nebylo by co zobrazit.");
            }

            for (int i = 0; i < (section.Items?.Count ?? 0); i++)
            {
                PriceItem item = section.Items![i];
                string itemWhere = IsBlank(item.Name) ? $"{where}, položka {i + 1}" : $"{where}, položka „{item.Name}“";

                if (IsBlank(item.Name))
                {
                    errors.Add($"{itemWhere}: chybí název (\"name\").");
                }

                if (item.Price is null && item.PriceText is null)
                {
                    errors.Add($"{itemWhere}: chybí cena (\"price\") nebo text místo ceny (\"priceText\").");
                }

                if (item.Price < 0)
                {
                    errors.Add($"{itemWhere}: záporná cena.");
                }
            }
        }

        return errors;
    }

    private static bool IsBlank(LocalizedText? text) =>
        text is null || string.IsNullOrWhiteSpace(text.Resolve(LocalizedText.FallbackLanguage));

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

public sealed record PriceSection(string? Id, LocalizedText Name, LocalizedText? Note, IReadOnlyList<PriceItem>? Items);

public sealed record PriceItem(
    LocalizedText Name,
    LocalizedText? Desc,
    decimal? Price,
    LocalizedText? PriceText,
    LocalizedText? Tag);
