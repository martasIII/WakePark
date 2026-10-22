namespace WakePark.Modules.Billing.Application.Abstractions;

/// <summary>
/// Registr firem (ARES). Údaje firmy na doklad se berou odsud, ne z formuláře — zákazník
/// zadá jen IČO.
/// </summary>
public interface ICompanyRegistry
{
    /// <returns>Null, když subjekt s tímto IČO v registru není.</returns>
    /// <exception cref="CompanyRegistryUnavailableException">Registr neodpovídá.</exception>
    Task<RegisteredCompany?> FindAsync(string companyId, CancellationToken cancellationToken = default);
}

/// <summary>Firma podle registru. DIČ jen u plátce DPH, datum zániku jen u zaniklé firmy.</summary>
public sealed record RegisteredCompany(string CompanyId, string Name, string? VatId, string? Address, DateOnly? DissolvedOn)
{
    public bool IsActive => DissolvedOn is null;
}

/// <summary>Registr firem neodpověděl (výpadek, údržba, timeout) — neznamená, že firma neexistuje.</summary>
public sealed class CompanyRegistryUnavailableException : Exception
{
    public CompanyRegistryUnavailableException()
    {
    }

    public CompanyRegistryUnavailableException(string message)
        : base(message)
    {
    }

    public CompanyRegistryUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
