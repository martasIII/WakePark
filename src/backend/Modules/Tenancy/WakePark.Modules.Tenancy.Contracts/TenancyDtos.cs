namespace WakePark.Modules.Tenancy.Contracts;

/// <summary>
/// Veřejný pohled na areál. Jediný tvar, který smí opustit modul Tenancy —
/// doménové entity zůstávají uvnitř.
/// </summary>
public sealed record ParkDto(
    Guid Id,
    Guid EnterpriseId,
    string Name,
    string? Description,
    string City,
    string CountryCode,
    string TimeZoneId,
    string Currency,
    CancellationPolicyDto CancellationPolicy);

/// <summary>Pravidla storna areálu tak, jak je vidí ostatní moduly.</summary>
/// <param name="CancellationAllowed">Zda areál storno vůbec umožňuje.</param>
/// <param name="FreeCancellationHours">Lhůta pro bezplatné storno v hodinách před začátkem slotu.</param>
/// <param name="LateCancellationRefundPercent">Kolik procent se vrací po uplynutí lhůty.</param>
/// <param name="NoShowRefundPercent">Kolik procent se vrací, když zákazník nedorazí.</param>
public sealed record CancellationPolicyDto(
    bool CancellationAllowed,
    int FreeCancellationHours,
    decimal LateCancellationRefundPercent,
    decimal NoShowRefundPercent);

/// <summary>Veřejný pohled na provozovatele.</summary>
public sealed record EnterpriseDto(
    Guid Id,
    string Name,
    string RegistrationNumber,
    string? VatNumber,
    string City,
    string CountryCode);
