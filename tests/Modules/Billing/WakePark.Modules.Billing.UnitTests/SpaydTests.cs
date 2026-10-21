using Shouldly;

using WakePark.Modules.Billing.Application.Payments;

namespace WakePark.Modules.Billing.UnitTests;

public sealed class SpaydTests
{
    [Fact]
    public void QR_obsahuje_ucet_castku_menu_VS_a_zpravu()
    {
        string spayd = Spayd.Build(
            "CZ95 2010 0000 0026 0133 8268", "FIOBCZPPXXX", 1290m, "CZK", "261000", "Wake Park objednavka 261000");

        spayd.ShouldBe(
            "SPD*1.0*ACC:CZ9520100000002601338268+FIOBCZPPXXX*AM:1290.00*CC:CZK*X-VS:261000*MSG:Wake Park objednavka 261000");
    }

    [Fact]
    public void Zprava_je_bez_diakritiky_bez_hvezdicky_a_nejvys_60_znaku()
    {
        string message = Spayd.Message("Objednávka *č. 261000* " + new string('x', 80));

        message.ShouldStartWith("Objednavka c. 261000 ");
        message.ShouldNotContain("*");
        message.Length.ShouldBe(Spayd.MessageMaxLength);
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("26A000")]
    public void Neplatny_variabilni_symbol_neprojde(string variableSymbol)
    {
        Should.Throw<ArgumentException>(() =>
            Spayd.Build("CZ9520100000002601338268", null, 100m, "CZK", variableSymbol, "x"));
    }
}
