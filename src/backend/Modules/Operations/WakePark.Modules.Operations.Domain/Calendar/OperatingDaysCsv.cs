using System.Globalization;

namespace WakePark.Modules.Operations.Domain.Calendar;

/// <summary>Jeden řádek CSV: den a otevírací doba. Bez časů = ten den zavřeno.</summary>
public sealed record CsvOperatingDay(DateOnly Date, TimeOnly? OpensAt, TimeOnly? ClosesAt)
{
    public bool IsClosed => OpensAt is null;
}

/// <summary>Výsledek čtení CSV. Chyby jsou česky s číslem řádku, pro člověka, který soubor připravil.</summary>
public sealed record CsvParseResult(IReadOnlyList<CsvOperatingDay> Days, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Provozní doba sezóny z CSV (ADR 0009): řádek <c>datum;od;do</c>, např. z Google Sheets.
/// </summary>
/// <remarks>
/// Benevolentní ke vstupu, přísný k obsahu: oddělovač středník, čárka i tabulátor; datum
/// <c>2027-05-01</c> i <c>1.5.2027</c>; čas <c>9:30</c> i <c>09:30:00</c>; první řádek smí být
/// hlavička. Ale den dvakrát, zavírání před otevřením nebo jen jeden z časů je chyba —
/// raději nic, než napůl špatná sezóna.
/// </remarks>
public static class OperatingDaysCsv
{
    public const int MaxRows = 400;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "d.M.yyyy", "d. M. yyyy", "d.M.yy"];
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss"];

    public static CsvParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<CsvOperatingDay> days = [];
        List<string> errors = [];
        HashSet<DateOnly> seen = [];

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        char separator = DetectSeparator(lines);
        bool first = true;

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim().TrimStart('﻿');
            int lineNumber = index + 1;

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] cells = [.. line.Split(separator).Select(c => c.Trim().Trim('"').Trim())];

            if (!TryParseDate(cells[0], out DateOnly date))
            {
                // Hlavička („datum;od;do“) smí být jen na prvním řádku s obsahem.
                if (!first)
                {
                    errors.Add($"Řádek {lineNumber}: „{cells[0]}“ není datum (např. 2027-05-01 nebo 1.5.2027).");
                }

                first = false;
                continue;
            }

            first = false;

            if (days.Count >= MaxRows)
            {
                errors.Add($"Soubor má víc než {MaxRows} dní — sezóna je kratší, nejspíš je v něm něco navíc.");
                break;
            }

            string opens = cells.Length > 1 ? cells[1] : string.Empty;
            string closes = cells.Length > 2 ? cells[2] : string.Empty;

            if (!seen.Add(date))
            {
                errors.Add($"Řádek {lineNumber}: den {date:d. M. yyyy} je v souboru víckrát.");
                continue;
            }

            if (opens.Length == 0 && closes.Length == 0)
            {
                days.Add(new CsvOperatingDay(date, null, null));
                continue;
            }

            if (!TryParseTime(opens, out TimeOnly opensAt) || !TryParseTime(closes, out TimeOnly closesAt))
            {
                errors.Add($"Řádek {lineNumber}: čekám otevírání i zavírání jako čas (např. 9:00;20:30), nebo obojí prázdné = zavřeno.");
                continue;
            }

            if (closesAt <= opensAt)
            {
                errors.Add($"Řádek {lineNumber}: {date:d. M. yyyy} se zavírá ({closes}) dřív, než otevírá ({opens}).");
                continue;
            }

            days.Add(new CsvOperatingDay(date, opensAt, closesAt));
        }

        if (days.Count == 0 && errors.Count == 0)
        {
            errors.Add("V souboru není žádný den.");
        }

        return new CsvParseResult([.. days.OrderBy(d => d.Date)], errors);
    }

    private static char DetectSeparator(string[] lines)
    {
        string sample = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? string.Empty;
        return sample.Contains(';', StringComparison.Ordinal) ? ';'
            : sample.Contains('\t', StringComparison.Ordinal) ? '\t'
            : ',';
    }

    private static bool TryParseDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
