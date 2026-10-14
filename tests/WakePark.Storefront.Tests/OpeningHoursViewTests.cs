using System.Globalization;

using WakePark.Storefront.Content;

namespace WakePark.Storefront.Tests;

public sealed class OpeningHoursViewTests
{
    private static readonly CultureInfo Czech = CultureInfo.GetCultureInfo("cs");

    // 7. 10. 2026 je středa.
    private static readonly DateOnly Wednesday = new(2026, 10, 7);

    [Fact]
    public void Dny_jsou_od_pondeli_v_jazyce_stranky_a_dnesek_je_zvyrazneny()
    {
        OpeningHoursView view = OpeningHoursView.Build(Hours(), Wednesday, Czech);

        view.Days.Select(d => d.Day).ShouldBe(["Pondělí", "Úterý", "Středa", "Čtvrtek", "Pátek", "Sobota", "Neděle"]);
        view.Days.Single(d => d.IsToday).Day.ShouldBe("Středa");
    }

    [Fact]
    public void Anglicky_jsou_nazvy_dnu_anglicky()
    {
        OpeningHoursView view = OpeningHoursView.Build(Hours(), Wednesday, CultureInfo.GetCultureInfo("en"));

        view.Days[0].Day.ShouldBe("Monday");
    }

    [Fact]
    public void Dnesni_vyjimka_prepise_bezny_den()
    {
        OpeningHoursView view = OpeningHoursView.Build(
            Hours(new OpeningOverride(Wednesday, Text("Zavřeno"), null)), Wednesday, Czech);

        view.Days.Single(d => d.IsToday).Hours.ShouldBe("Zavřeno");
    }

    [Fact]
    public void Minule_vyjimky_se_nezobrazuji_budouci_jsou_serazene()
    {
        OpeningHoursView view = OpeningHoursView.Build(
            Hours(
                new OpeningOverride(new DateOnly(2026, 12, 24), Text("Zavřeno"), Text("Štědrý den")),
                new OpeningOverride(new DateOnly(2026, 10, 1), Text("Zavřeno"), null),
                new OpeningOverride(new DateOnly(2026, 10, 28), Text("Zavřeno"), Text("Státní svátek"))),
            Wednesday,
            Czech);

        view.Exceptions.Select(e => e.Date).ShouldBe(["st 28. 10.", "čt 24. 12."]);
        view.Exceptions[0].Note.ShouldBe("Státní svátek");
    }

    private static LocalizedText Text(string text) => LocalizedText.Invariant(text);

    private static OpeningHours Hours(params OpeningOverride[] exceptions) => new(
        OpeningHours.DayKeys.ToDictionary(day => day, _ => Text("10:00–20:00")),
        exceptions,
        null);
}
