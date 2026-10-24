using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Catalog.Application.Catalog;

/// <summary>Ceník na webu s dnes platnými cenami. Veřejný, stejně jako nabídka e-shopu.</summary>
public sealed record GetPublicPriceListQuery(Guid EnterpriseId) : IQuery<PublicPriceListDto>;

internal sealed class GetPublicPriceListQueryHandler(ICatalogReadRepository repository, CatalogCalendar calendar)
    : IQueryHandler<GetPublicPriceListQuery, PublicPriceListDto>
{
    public async Task<PublicPriceListDto> HandleAsync(GetPublicPriceListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        CatalogSnapshot snapshot = await repository
            .GetSnapshotAsync(query.EnterpriseId, cancellationToken)
            .ConfigureAwait(false);

        return CatalogViews.PriceList(snapshot, calendar.Today);
    }
}
