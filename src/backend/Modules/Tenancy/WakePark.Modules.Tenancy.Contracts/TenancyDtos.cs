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
    string Currency);

/// <summary>Veřejný pohled na provozovatele.</summary>
public sealed record EnterpriseDto(
    Guid Id,
    string Name,
    string RegistrationNumber,
    string? VatNumber,
    string City,
    string CountryCode);
