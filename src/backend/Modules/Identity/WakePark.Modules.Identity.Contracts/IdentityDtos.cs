namespace WakePark.Modules.Identity.Contracts;

/// <summary>Kdo je přihlášený a co smí.</summary>
/// <param name="UserAccountId">Identifikátor účtu.</param>
/// <param name="Email">Přihlašovací e-mail.</param>
/// <param name="EnterpriseId">
/// Provozovatel, pod kterého uživatel spadá. Null u účtu, kterému zatím nikdo nepřidělil roli.
/// </param>
/// <param name="Parks">Areály, ke kterým má uživatel přístup, i s oprávněními v každém z nich.</param>
/// <param name="GlobalPermissions">Oprávnění platná ve všech areálech provozovatele.</param>
public sealed record CurrentUserDto(
    Guid UserAccountId,
    string Email,
    Guid? EnterpriseId,
    IReadOnlyList<ParkAccessDto> Parks,
    IReadOnlyList<string> GlobalPermissions);

/// <summary>Přístup uživatele do jednoho areálu.</summary>
public sealed record ParkAccessDto(
    Guid WakeParkId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
