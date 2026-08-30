using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Operations.Application.Schedule;
using WakePark.Modules.Operations.Contracts;

namespace WakePark.Modules.Operations.Application;

/// <summary>Registrace aplikační vrstvy modulu Operations.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddOperationsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IOperationsSchedule, ScheduleService>();
        services.AddScoped<ICableDirectory, CableDirectory>();
        services.AddScoped<
            IQueryHandler<GetScheduleQuery, IReadOnlyList<SlotTemplateDto>>,
            GetScheduleQueryHandler>();

        return services;
    }
}
