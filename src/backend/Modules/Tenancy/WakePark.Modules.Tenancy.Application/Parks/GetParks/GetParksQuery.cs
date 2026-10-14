using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Application.Parks.GetParks;

/// <summary>
/// Areály zadaného provozovatele. Cizí provozovatel vrátí prázdný seznam —
/// stejně jako neexistující, aby z odpovědi nešlo poznat, že existuje.
/// </summary>
public sealed record GetParksQuery(Guid EnterpriseId) : IQuery<IReadOnlyList<ParkDto>>;

/// <summary>Areály provozovatele, pod kterého spadá přihlášený uživatel.</summary>
public sealed record GetTenantParksQuery : IQuery<IReadOnlyList<ParkDto>>;
