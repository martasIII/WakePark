namespace WakePark.Storefront.Content;

/// <summary>Otevírací doba (<c>data/oteviraci-doba.json</c>).</summary>
/// <param name="Week">Klíče <c>mon</c> až <c>sun</c>. Názvy dnů dodá web v jazyce stránky.</param>
/// <param name="Exceptions">Výjimky na konkrétní datum. Minulé se nezobrazují, mazat je netřeba.</param>
/// <param name="Note">Poznámka pod tabulkou.</param>
public sealed record OpeningHours(
    IReadOnlyDictionary<string, LocalizedText> Week,
    IReadOnlyList<OpeningOverride>? Exceptions,
    LocalizedText? Note) : IValidatedContent
{
    /// <summary>Klíče dnů v pořadí od pondělí.</summary>
    public static readonly IReadOnlyList<string> DayKeys = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (Week is null)
        {
            errors.Add("Chybí týdenní rozpis (\"week\").");
            return errors;
        }

        errors.AddRange(DayKeys
            .Where(day => !Week.ContainsKey(day))
            .Select(day => $"V týdenním rozpisu chybí den „{day}“."));

        errors.AddRange(Week.Keys
            .Where(key => !DayKeys.Contains(key))
            .Select(key => $"Neznámý den „{key}“ — použij {string.Join(", ", DayKeys)}."));

        errors.AddRange((Exceptions ?? [])
            .GroupBy(e => e.Date)
            .Where(g => g.Count() > 1)
            .Select(g => $"Výjimka na {g.Key:yyyy-MM-dd} je zadaná víckrát."));

        return errors;
    }
}

/// <summary>Jiná otevírací doba v konkrétní den (svátek, akce, počasí).</summary>
/// <param name="Date">Datum ve tvaru <c>RRRR-MM-DD</c>.</param>
/// <param name="Hours">Otevírací doba v ten den, např. „Zavřeno“.</param>
/// <param name="Note">Důvod, zobrazí se vedle data.</param>
public sealed record OpeningOverride(DateOnly Date, LocalizedText Hours, LocalizedText? Note);
