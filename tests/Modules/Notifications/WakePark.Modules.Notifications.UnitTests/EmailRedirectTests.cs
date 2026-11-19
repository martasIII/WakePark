using Shouldly;

using WakePark.Modules.Notifications.Application;

namespace WakePark.Modules.Notifications.UnitTests;

/// <summary>V testovacím prostředí (UAT) nesmí e-mail odejít zákazníkovi — jen správcům, s původním adresátem v předmětu.</summary>
public sealed class EmailRedirectTests
{
    [Fact]
    public void Bez_presmerovani_jde_e_mail_skutecnemu_adresatovi()
    {
        (IReadOnlyList<string> to, string subject) = new NotificationsOptions().Route("jan@novak.cz", "Objednávka 2027001");

        to.ShouldBe(["jan@novak.cz"]);
        subject.ShouldBe("Objednávka 2027001");
    }

    [Fact]
    public void S_presmerovanim_jde_jen_spravcum_a_predmet_prozradi_puvodniho_adresata()
    {
        NotificationsOptions options = new() { RedirectTo = ["martin@example.cz", "dan@example.cz"], RedirectLabel = "UAT" };

        (IReadOnlyList<string> to, string subject) = options.Route("jan@novak.cz", "Objednávka 2027001");

        to.ShouldBe(["martin@example.cz", "dan@example.cz"]);
        subject.ShouldBe("[UAT → jan@novak.cz] Objednávka 2027001");
    }
}
