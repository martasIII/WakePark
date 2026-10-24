using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Catalog.Application.Catalog;

/// <summary>Nabídka e-shopu provozovatele s dnes platnými cenami.</summary>
/// <remarks>
/// Je veřejná, proto se neomezuje na provozovatele přihlášeného uživatele (srv. ADR 0007) —
/// nabídka je stejná pro každého a nic neprozradí.
/// </remarks>
public sealed record GetCatalogQuery(Guid EnterpriseId) : IQuery<CatalogDto>;

internal sealed class GetCatalogQueryHandler(ICatalogReader reader) : IQueryHandler<GetCatalogQuery, CatalogDto>
{
    public Task<CatalogDto> HandleAsync(GetCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return reader.GetPublishedCatalogAsync(query.EnterpriseId, cancellationToken);
    }
}

/// <inheritdoc />
internal sealed class CatalogReader(ICatalogReadRepository repository, CatalogCalendar calendar) : ICatalogReader
{
    public async Task<CatalogDto> GetPublishedCatalogAsync(Guid enterpriseId, CancellationToken cancellationToken = default)
    {
        CatalogSnapshot snapshot = await repository.GetSnapshotAsync(enterpriseId, cancellationToken).ConfigureAwait(false);
        return CatalogViews.Eshop(snapshot, calendar.Today);
    }
}
