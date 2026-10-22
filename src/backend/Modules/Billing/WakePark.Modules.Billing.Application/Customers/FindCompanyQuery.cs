using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Domain.Customers;

namespace WakePark.Modules.Billing.Application.Customers;

/// <summary>
/// Firma podle IČO — pro náhled ve formuláři e-shopu. Neplatné IČO, neexistující i zaniklá
/// firma vrátí <c>null</c>; výpadek registru projde jako <see cref="CompanyRegistryUnavailableException"/>.
/// </summary>
public sealed record FindCompanyQuery(string CompanyId) : IQuery<CompanyDto?>;

internal sealed class FindCompanyQueryHandler(ICompanyRegistry registry) : IQueryHandler<FindCompanyQuery, CompanyDto?>
{
    public async Task<CompanyDto?> HandleAsync(FindCompanyQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        string companyId = (query.CompanyId ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!CustomerContact.IsValidCompanyId(companyId))
        {
            return null;
        }

        RegisteredCompany? company = await registry.FindAsync(companyId, cancellationToken).ConfigureAwait(false);

        return company is { IsActive: true }
            ? new CompanyDto(company.CompanyId, company.Name, company.VatId, company.Address)
            : null;
    }
}
