using Shouldly;

using WakePark.Modules.Operations.Domain.Calendar;

namespace WakePark.Modules.Operations.UnitTests.Calendar;

/// <summary>CSV s provozní dobou sezóny, jak ho Martin připraví v Google Sheets (ADR 0009).</summary>
public sealed class OperatingDaysCsvTests
{
    [Fact]
    public void Prijme_hlavicku_strednik_ceske_datum_i_sekundy_v_case()
    {
        CsvParseResult result = OperatingDaysCsv.Parse("""
            datum;od;do
            1.5.2027;10:00;19:30
            2027-05-02;09:00:00;20:00:00
            """);

        result.Errors.ShouldBeEmpty();
        result.Days.ShouldBe(
        [
            new CsvOperatingDay(new DateOnly(2027, 5, 1), new TimeOnly(10, 0), new TimeOnly(19, 30)),
            new CsvOperatingDay(new DateOnly(2027, 5, 2), new TimeOnly(9, 0), new TimeOnly(20, 0)),
        ]);
    }

    [Fact]
    public void Carka_jako_oddelovac_a_prazdne_casy_znamenaji_zavreno()
    {
        CsvParseResult result = OperatingDaysCsv.Parse("2027-05-03,,\r\n2027-05-04,10:00,18:00\r\n");

        result.Errors.ShouldBeEmpty();
        result.Days[0].IsClosed.ShouldBeTrue();
        result.Days[1].OpensAt.ShouldBe(new TimeOnly(10, 0));
    }

    [Fact]
    public void Chyby_se_hlasi_s_cislem_radku_a_den_navic_neprojde()
    {
        CsvParseResult result = OperatingDaysCsv.Parse("""
            datum;od;do
            1.5.2027;10:00;19:30
            1.5.2027;10:00;19:30
            2.5.2027;20:00;10:00
            3.5.2027;10:00;
            pondělí;10:00;19:00
            """);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(4);
        result.Errors[0].ShouldStartWith("Řádek 3:");
        result.Errors[1].ShouldContain("dřív, než otevírá");
        result.Errors[2].ShouldStartWith("Řádek 5:");
        result.Errors[3].ShouldContain("„pondělí“ není datum");
    }

    [Fact]
    public void Prazdny_soubor_je_chyba()
    {
        OperatingDaysCsv.Parse("datum;od;do\n").Errors.ShouldHaveSingleItem().ShouldContain("žádný den");
    }
}
