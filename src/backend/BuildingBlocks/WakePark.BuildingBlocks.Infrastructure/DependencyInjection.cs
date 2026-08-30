using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.BuildingBlocks.Infrastructure.Time;

namespace WakePark.BuildingBlocks.Infrastructure;

/// <summary>Registrace sdílené infrastruktury. Volá se jednou z hostu, ne z modulů.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddScoped<AuditInterceptor>();

        return services;
    }
}
