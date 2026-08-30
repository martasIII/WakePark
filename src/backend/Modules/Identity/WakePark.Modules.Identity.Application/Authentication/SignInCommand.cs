using WakePark.BuildingBlocks.Application.Messaging;

namespace WakePark.Modules.Identity.Application.Authentication;

/// <summary>Ověření přihlašovacích údajů.</summary>
public sealed record SignInCommand(string Email, string Password) : ICommand<SignedInUser>;

/// <summary>Co o uživateli potřebuje host, aby mu mohl vystavit cookie.</summary>
/// <param name="UserAccountId">Identifikátor účtu.</param>
/// <param name="Email">Přihlašovací e-mail.</param>
/// <param name="SecurityStamp">
/// Otisk stavu účtu. Uloží se do cookie a při každém požadavku se porovná s databází, takže
/// změna hesla nebo zablokování okamžitě zneplatní existující přihlášení.
/// </param>
public sealed record SignedInUser(Guid UserAccountId, string Email, string SecurityStamp);
