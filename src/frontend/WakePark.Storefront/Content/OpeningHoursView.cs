using System.Globalization;

namespace WakePark.Storefront.Content;

/// <summary>
/// Otevírací doba připravená k zobrazení pro konkrétní den a jazyk: názvy dnů v jazyce
/// stránky, zvýrazněný dnešek a jen dnešní a budoucí výjimky.
/// </summary>
public sealed record OpeningHoursView(
    IReadOnlyList<OpeningHoursView.DayRow> Days,
    IReadOnlyList<OpeningHoursView.ExceptionRow> Exceptions,
    string? Note)
{
    public sealed record DayRow(string Day, string Hours, bool IsToday);

    public sealed record ExceptionRow(string Date, string Hours, string? Note);

    public static OpeningHoursView Build(OpeningHours hours, DateOnly today, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(hours);
        ArgumentNullException.ThrowIfNull(culture);

        string language = culture.TwoLetterISOLanguageName;

        List<OpeningOverride> upcoming = [.. (hours.Exceptions ?? [])
            .Where(e => e.Date >= today)
            .OrderBy(e => e.Date)];
        OpeningOverride? todayException = upcoming.Find(e => e.Date == today);

        // DayOfWeek začíná nedělí, náš týden pondělím.
        int todayIndex = ((int)today.DayOfWeek + 6) % 7;

        List<DayRow> days = [.. OpeningHours.DayKeys.Select((key, index) =>
        {
            bool isToday = index == todayIndex;
            LocalizedText text = isToday && todayException is not null ? todayException.Hours : hours.Week[key];
            DayOfWeek dayOfWeek = (DayOfWeek)((index + 1) % 7);
            return new DayRow(Capitalize(culture.DateTimeFormat.GetDayName(dayOfWeek), culture), text.Resolve(language), isToday);
        })];

        List<ExceptionRow> exceptions = [.. upcoming.Select(e => new ExceptionRow(
            FormatDate(e.Date, culture),
            e.Hours.Resolve(language),
            e.Note?.Resolve(language)))];

        return new OpeningHoursView(days, exceptions, hours.Note?.Resolve(language));
    }

    /// <summary>Krátký den v týdnu a datum bez roku: „st 28. 10.“, „Wed 28/10“.</summary>
    private static string FormatDate(DateOnly date, CultureInfo culture)
    {
        string day = culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek);
        string monthDay = culture.TwoLetterISOLanguageName == "cs"
            ? date.ToString("d. M.", culture)
            : date.ToString(culture.DateTimeFormat.MonthDayPattern, culture);
        return $"{day} {monthDay}";
    }

    private static string Capitalize(string text, CultureInfo culture) =>
        text.Length == 0 ? text : char.ToUpper(text[0], culture) + text[1..];
}
