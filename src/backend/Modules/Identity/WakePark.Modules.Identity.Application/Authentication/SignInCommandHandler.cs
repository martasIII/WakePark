using Microsoft.Extensions.Logging;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Domain.Users;

namespace WakePark.Modules.Identity.Application.Authentication;

/// <summary>
/// Ověří e-mail a heslo.
/// </summary>
/// <remarks>
/// Navenek se všechny nezdary tváří stejně. Kdyby se lišila odpověď pro neznámý účet
/// a pro špatné heslo, dal by se seznam existujících e-mailů uhodnout zkoušením.
/// Ze stejného důvodu se u neznámého účtu hashuje zahazovací heslo — bez toho by odpověď
/// přišla znatelně rychleji a prozradila by totéž časem.
/// </remarks>
internal sealed partial class SignInCommandHandler(
    IUserAccountRepository repository,
    IPasswordHasher passwordHasher,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    ILogger<SignInCommandHandler> logger) : ICommandHandler<SignInCommand, SignedInUser>
{
    /// <summary>Otisk neexistujícího hesla, aby ověření neznámého účtu trvalo srovnatelně dlouho.</summary>
    private const string DummyHash =
        "AQAAAAIAAYagAAAAEG4jvKq0dZmVn8fBqQm0Uu5N7YtQmm6nWZkTr5nJDsXhLxNbHHfjBl6qMBnKMdmVDA==";

    private static readonly DomainException InvalidCredentials =
        new("auth.invalid_credentials", "Nesprávný e-mail nebo heslo.");

    public async Task<SignedInUser> HandleAsync(
        SignInCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            throw InvalidCredentials;
        }

        string email = UserAccount.NormalizeEmail(command.Email);

        UserAccount? account = await repository
            .FindByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            _ = passwordHasher.Verify(DummyHash, command.Password);
            LogUnknownAccount(logger, email);
            throw InvalidCredentials;
        }

        if (!account.CanSignIn)
        {
            LogNotAllowed(logger, account.Id, account.Status);
            throw InvalidCredentials;
        }

        PasswordVerification verification = passwordHasher.Verify(account.PasswordHash, command.Password);

        if (verification is PasswordVerification.Failed)
        {
            account.RecordFailedLogin();
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            LogWrongPassword(logger, account.Id, account.FailedLoginAttempts);
            throw InvalidCredentials;
        }

        if (verification is PasswordVerification.SuccessRehashNeeded)
        {
            // Heslo je správné, jen otisk vznikl slabším nastavením. Přepočítáme ho, dokud
            // heslo držíme v ruce — jindy už k němu přístup nemáme.
            account.SetPassword(passwordHasher.Hash(command.Password));
        }

        account.RecordSuccessfulLogin(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SignedInUser(account.Id, account.Email, account.SecurityStamp);
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information,
        Message = "Přihlášení odmítnuto: účet {Email} neexistuje.")]
    private static partial void LogUnknownAccount(ILogger logger, string email);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning,
        Message = "Přihlášení odmítnuto: účet {UserAccountId} je ve stavu {Status}.")]
    private static partial void LogNotAllowed(ILogger logger, Guid userAccountId, UserAccountStatus status);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "Přihlášení odmítnuto: špatné heslo k účtu {UserAccountId}, neúspěšných pokusů {Attempts}.")]
    private static partial void LogWrongPassword(ILogger logger, Guid userAccountId, int attempts);
}
