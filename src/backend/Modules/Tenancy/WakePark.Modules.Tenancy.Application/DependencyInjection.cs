using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Tenancy.Application.Parks.GetParks;
using WakePark.Modules.Tenancy.Contracts;

namespace WakePark.Modules.Tenancy.Application;

/// <summary>Registrace aplikační vrstvy modulu Tenancy.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTenancyApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IQueryHandler<GetParksQuery, IReadOnlyList<ParkDto>>, GetParksQueryHandler>();
        services.AddScoped<IQueryHandler<GetTenantParksQuery, IReadOnlyList<ParkDto>>, GetTenantParksQueryHandler>();

        return services;
    }
}
