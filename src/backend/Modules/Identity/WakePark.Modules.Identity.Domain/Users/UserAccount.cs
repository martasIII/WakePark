using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Identity.Domain.Users;

/// <summary>
/// Přihlašovací účet.
/// </summary>
/// <remarks>
/// Záměrně neobsahuje osobní údaje — jméno, telefon ani adresa sem nepatří. Ty jsou
/// u zákazníka (<c>Customers</c>) nebo zaměstnance (<c>Staff</c>). Účet je jen klíč od dveří.
/// </remarks>
public sealed class UserAccount : AggregateRoot, IAuditable
{
    /// <summary>Po kolika chybných pokusech se účet zamkne.</summary>
    private const int MaxFailedAttempts = 5;

    private UserAccount()
    {
    }

    private UserAccount(Guid id, string email, string passwordHash)
        : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        SecurityStamp = Guid.CreateVersion7().ToString("N");
        Status = UserAccountStatus.Active;
    }

    /// <summary>Přihlašovací identifikátor. Vždy malými písmeny, aby velikost nerozhodovala.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Otisk hesla. Do logů ani do DTO se nikdy nedostane.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Mění se při každé změně hesla nebo zablokování. Umožňuje zneplatnit existující
    /// přihlášení, aniž by se musela mazat session na serveru.
    /// </summary>
    public string SecurityStamp { get; private set; } = string.Empty;

    public UserAccountStatus Status { get; private set; }

    public DateTimeOffset? EmailConfirmedAtUtc { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static UserAccount Create(string email, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        string normalized = NormalizeEmail(email);

        if (!normalized.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainException("user_account.invalid_email", "E-mail nemá platný tvar.");
        }

        return new UserAccount(NewId(), normalized, passwordHash);
    }

    /// <summary>Sjednocení e-mailu, aby se stejný účet nedal založit dvakrát jinou velikostí písmen.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public bool CanSignIn => Status is UserAccountStatus.Active;

    public void RecordSuccessfulLogin(DateTimeOffset atUtc)
    {
        FailedLoginAttempts = 0;
        LastLoginAtUtc = atUtc;
    }

    /// <summary>
    /// Zaznamená chybné heslo a po překročení limitu účet zamkne. Odemknout ho musí obsluha —
    /// automatické odemykání po čase by útočníkovi jen prodloužilo pokusy.
    /// </summary>
    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= MaxFailedAttempts && Status is UserAccountStatus.Active)
        {
            Status = UserAccountStatus.Locked;
        }
    }

    public void SetPassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        RotateSecurityStamp();
    }

    public void Unlock()
    {
        FailedLoginAttempts = 0;

        if (Status is UserAccountStatus.Locked)
        {
            Status = UserAccountStatus.Active;
        }
    }

    public void Suspend()
    {
        Status = UserAccountStatus.Suspended;
        RotateSecurityStamp();
    }

    public void ConfirmEmail(DateTimeOffset atUtc) => EmailConfirmedAtUtc ??= atUtc;

    private void RotateSecurityStamp() => SecurityStamp = Guid.CreateVersion7().ToString("N");
}
