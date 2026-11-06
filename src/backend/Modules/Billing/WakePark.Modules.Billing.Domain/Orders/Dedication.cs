using System.Text;

namespace WakePark.Modules.Billing.Domain.Orders;

/// <summary>
/// Věnování na poukaz (ADR 0006): nepovinné, max. <see cref="MaxLength"/> znaků, jen latinková
/// písmena (i s diakritikou evropských jazyků), číslice, mezery a běžná interpunkce. Emoji ani
/// jiná písma v písmu poukazu nejsou — v PDF by z nich byly čtverečky.
/// </summary>
public static class Dedication
{
    public const int MaxLength = 120;

    private const string Punctuation = ".,!?;:'\"„“”‚‘’«»-–—()/&+%*#@…";

    /// <summary>
    /// Sjednotí zápis: složené znaky (NFC), bílé znaky včetně konců řádků na jednu mezeru,
    /// bez mezer na krajích. Prázdný text je <see cref="string.Empty"/>.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder result = new(text.Length);
        bool space = false;
        foreach (char c in text.Normalize(NormalizationForm.FormC).Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                result.Append(' ');
                space = false;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>Platné věnování — už <see cref="Normalize"/>-ované. Prázdné je platné (bez věnování).</summary>
    public static bool IsValid(string normalized)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        return normalized.Length <= MaxLength && normalized.All(IsAllowed);
    }

    /// <summary>Latinka do Latin Extended-B (U+024F) pokryje evropské jazyky psané latinkou.</summary>
    private static bool IsAllowed(char c) =>
        c == ' '
        || char.IsAsciiDigit(c)
        || (char.IsLetter(c) && c <= 'ɏ')
        || Punctuation.Contains(c, StringComparison.Ordinal);
}
