using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Identity.Domain.Users;

namespace WakePark.Modules.Identity.UnitTests.Domain;

public sealed class UserAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private static UserAccount NewAccount() => UserAccount.Create("Admin@Wake-Dzban.CZ", "hash");

    [Fact]
    public void Email_se_uklada_normalizovany()
    {
        NewAccount().Email.ShouldBe("admin@wake-dzban.cz");
    }

    [Fact]
    public void Ucet_bez_zavinace_neprojde()
    {
        Should.Throw<DomainException>(() => UserAccount.Create("neni-email", "hash"))
            .Code.ShouldBe("user_account.invalid_email");
    }

    [Fact]
    public void Pet_chybnych_pokusu_ucet_zamkne()
    {
        UserAccount account = NewAccount();

        for (int i = 0; i < 5; i++)
        {
            account.RecordFailedLogin();
        }

        account.Status.ShouldBe(UserAccountStatus.Locked);
        account.CanSignIn.ShouldBeFalse();
    }

    [Fact]
    public void Ctyri_chybne_pokusy_jeste_nestaci()
    {
        UserAccount account = NewAccount();

        for (int i = 0; i < 4; i++)
        {
            account.RecordFailedLogin();
        }

        account.CanSignIn.ShouldBeTrue();
    }

    [Fact]
    public void Uspesne_prihlaseni_vynuluje_pocitadlo()
    {
        UserAccount account = NewAccount();
        account.RecordFailedLogin();
        account.RecordFailedLogin();

        account.RecordSuccessfulLogin(Now);

        account.FailedLoginAttempts.ShouldBe(0);
        account.LastLoginAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Zmena_hesla_zneplatni_existujici_prihlaseni()
    {
        UserAccount account = NewAccount();
        string original = account.SecurityStamp;

        account.SetPassword("novy-hash");

        account.SecurityStamp.ShouldNotBe(original);
    }

    [Fact]
    public void Pozastaveni_uctu_take_zneplatni_prihlaseni()
    {
        UserAccount account = NewAccount();
        string original = account.SecurityStamp;

        account.Suspend();

        account.CanSignIn.ShouldBeFalse();
        account.SecurityStamp.ShouldNotBe(original);
    }

    [Fact]
    public void Odemceni_vrati_ucet_do_provozu()
    {
        UserAccount account = NewAccount();
        for (int i = 0; i < 5; i++)
        {
            account.RecordFailedLogin();
        }

        account.Unlock();

        account.CanSignIn.ShouldBeTrue();
        account.FailedLoginAttempts.ShouldBe(0);
    }
}
