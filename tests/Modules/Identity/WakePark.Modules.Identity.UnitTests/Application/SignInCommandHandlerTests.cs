using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Application.Authentication;
using WakePark.Modules.Identity.Domain.Users;

namespace WakePark.Modules.Identity.UnitTests.Application;

/// <summary>
/// Přihlašování je místo, kde se chyba projeví jako bezpečnostní díra, ne jako spadlý test
/// v provozu. Proto se tu hlídá i to, co uživatel nevidí — hlavně že se odpovědi neliší.
/// </summary>
public sealed class SignInCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly IUserAccountRepository _repository = Substitute.For<IUserAccountRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public SignInCommandHandlerTests() => _clock.UtcNow.Returns(Now);

    private SignInCommandHandler CreateHandler() =>
        new(_repository, _hasher, _unitOfWork, _clock, NullLogger<SignInCommandHandler>.Instance);

    private UserAccount GivenAccount(string email = "admin@wake-dzban.cz")
    {
        UserAccount account = UserAccount.Create(email, "ulozeny-hash");
        _repository.FindByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(account);
        return account;
    }

    [Fact]
    public async Task Spravne_heslo_vrati_uzivatele_a_zaznamena_prihlaseni()
    {
        UserAccount account = GivenAccount();
        _hasher.Verify("ulozeny-hash", "heslo").Returns(PasswordVerification.Success);

        SignedInUser result = await CreateHandler()
            .HandleAsync(new SignInCommand("admin@wake-dzban.cz", "heslo"), TestContext.Current.CancellationToken);

        result.UserAccountId.ShouldBe(account.Id);
        result.Email.ShouldBe("admin@wake-dzban.cz");
        account.LastLoginAtUtc.ShouldBe(Now);
    }

    [Fact]
    public async Task Velikost_pismen_v_emailu_nerozhoduje()
    {
        GivenAccount();
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(PasswordVerification.Success);

        await CreateHandler()
            .HandleAsync(new SignInCommand("  Admin@Wake-Dzban.CZ ", "heslo"), TestContext.Current.CancellationToken);

        await _repository.Received(1)
            .FindByEmailAsync("admin@wake-dzban.cz", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Neznamy_ucet_a_spatne_heslo_selzou_stejne()
    {
        _repository.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((UserAccount?)null);

        DomainException unknown = await Should.ThrowAsync<DomainException>(
            () => CreateHandler().HandleAsync(new SignInCommand("nikdo@nikde.cz", "heslo")));

        GivenAccount();
        _hasher.Verify("ulozeny-hash", "heslo").Returns(PasswordVerification.Failed);

        DomainException wrongPassword = await Should.ThrowAsync<DomainException>(
            () => CreateHandler().HandleAsync(new SignInCommand("admin@wake-dzban.cz", "heslo")));

        unknown.Code.ShouldBe(wrongPassword.Code);
        unknown.Message.ShouldBe(wrongPassword.Message);
    }

    [Fact]
    public async Task U_neznameho_uctu_se_presto_hashuje()
    {
        _repository.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((UserAccount?)null);

        await Should.ThrowAsync<DomainException>(
            () => CreateHandler().HandleAsync(new SignInCommand("nikdo@nikde.cz", "heslo")));

        // Bez tohohle by odpověď pro neexistující účet přišla znatelně dřív
        // a prozradila by, které e-maily v systému jsou.
        _hasher.Received(1).Verify(Arg.Any<string>(), "heslo");
    }

    [Fact]
    public async Task Spatne_heslo_se_zapocita_a_ulozi()
    {
        UserAccount account = GivenAccount();
        _hasher.Verify("ulozeny-hash", "heslo").Returns(PasswordVerification.Failed);

        await Should.ThrowAsync<DomainException>(
            () => CreateHandler().HandleAsync(new SignInCommand("admin@wake-dzban.cz", "heslo")));

        account.FailedLoginAttempts.ShouldBe(1);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Zamceny_ucet_se_neprihlasi_ani_spravnym_heslem()
    {
        UserAccount account = GivenAccount();
        account.Suspend();
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(PasswordVerification.Success);

        DomainException exception = await Should.ThrowAsync<DomainException>(
            () => CreateHandler().HandleAsync(new SignInCommand("admin@wake-dzban.cz", "heslo")));

        exception.Code.ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Zastaraly_otisk_hesla_se_pri_prihlaseni_prepocita()
    {
        UserAccount account = GivenAccount();
        _hasher.Verify("ulozeny-hash", "heslo").Returns(PasswordVerification.SuccessRehashNeeded);
        _hasher.Hash("heslo").Returns("novy-silnejsi-hash");

        await CreateHandler()
            .HandleAsync(new SignInCommand("admin@wake-dzban.cz", "heslo"), TestContext.Current.CancellationToken);

        account.PasswordHash.ShouldBe("novy-silnejsi-hash");
    }

    [Fact]
    public async Task Prazdne_heslo_se_ani_neoveruje()
    {
        await Should.ThrowAsync<DomainException>(
            () => CreateHandler().HandleAsync(new SignInCommand("admin@wake-dzban.cz", "")));

        await _repository.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
