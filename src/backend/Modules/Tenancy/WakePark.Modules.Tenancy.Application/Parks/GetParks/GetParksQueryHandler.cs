using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Tenancy.Application.Abstractions;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Application.Parks.GetParks;

internal sealed class GetParksQueryHandler(IParkReadRepository repository)
    : IQueryHandler<GetParksQuery, IReadOnlyList<ParkDto>>
{
    public Task<IReadOnlyList<ParkDto>> HandleAsync(
        GetParksQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return repository.GetAllAsync(query.EnterpriseId, cancellationToken);
    }
}
