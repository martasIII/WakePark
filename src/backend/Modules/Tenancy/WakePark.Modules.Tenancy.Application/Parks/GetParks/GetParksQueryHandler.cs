using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Tenancy.Application.Abstractions;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Application.Parks.GetParks;

/// <remarks>
/// Omezení na provozovatele je tady, ne v repozitáři ani v globálním filtru EF —
/// viz ADR 0007. Autorizace u výpisů bez areálu pustí každého, kdo má oprávnění
/// v kterémkoli areálu; data si proto musí omezit dotaz sám.
/// </remarks>
internal sealed class GetParksQueryHandler(IParkReadRepository repository, ITenantContext tenant)
    : IQueryHandler<GetParksQuery, IReadOnlyList<ParkDto>>
{
    public Task<IReadOnlyList<ParkDto>> HandleAsync(
        GetParksQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return tenant.EnterpriseId == query.EnterpriseId
            ? repository.GetByEnterpriseAsync(query.EnterpriseId, cancellationToken)
            : Task.FromResult<IReadOnlyList<ParkDto>>([]);
    }
}

/// <inheritdoc cref="GetParksQueryHandler" />
internal sealed class GetTenantParksQueryHandler(IParkReadRepository repository, ITenantContext tenant)
    : IQueryHandler<GetTenantParksQuery, IReadOnlyList<ParkDto>>
{
    public Task<IReadOnlyList<ParkDto>> HandleAsync(
        GetTenantParksQuery query,
        CancellationToken cancellationToken = default) =>
        // Bez provozovatele nic — raději prázdný seznam než všechno.
        tenant.EnterpriseId is { } enterpriseId
            ? repository.GetByEnterpriseAsync(enterpriseId, cancellationToken)
            : Task.FromResult<IReadOnlyList<ParkDto>>([]);
}
