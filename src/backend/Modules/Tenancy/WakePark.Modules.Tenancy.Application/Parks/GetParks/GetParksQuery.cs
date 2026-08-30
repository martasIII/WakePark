using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Application.Parks.GetParks;

/// <summary>Seznam areálů jednoho provozovatele.</summary>
public sealed record GetParksQuery(Guid EnterpriseId) : IQuery<IReadOnlyList<ParkDto>>;
