using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Catalog.Application.Catalog;

/// <summary>
/// Nabídka e-shopu provozovatele: jen aktivní produkty s cenou a kategorie, které nějaký mají.
/// </summary>
/// <remarks>
/// Je veřejná, proto se neomezuje na provozovatele přihlášeného uživatele (srv. ADR 0007) —
/// nabídka je stejná pro každého a nic neprozradí.
/// </remarks>
public sealed record GetCatalogQuery(Guid EnterpriseId) : IQuery<CatalogDto>;

internal sealed class GetCatalogQueryHandler(ICatalogReadRepository repository)
    : IQueryHandler<GetCatalogQuery, CatalogDto>
{
    public Task<CatalogDto> HandleAsync(GetCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return repository.GetPublishedCatalogAsync(query.EnterpriseId, cancellationToken);
    }
}
