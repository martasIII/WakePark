using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Operations.Application.Calendar;
using WakePark.Modules.Operations.Application.Schedule;
using WakePark.Modules.Operations.Application.Setup;
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

        services.AddScoped<ParkAccess>();
        services.AddScoped<IQueryHandler<GetPublicOpeningHoursQuery, PublicOpeningHoursDto>, GetPublicOpeningHoursQueryHandler>();
        services.AddScoped<IQueryHandler<GetOperatingCalendarQuery, OperatingCalendarDto>, GetOperatingCalendarQueryHandler>();
        services.AddScoped<ICommandHandler<SetOperatingDayCommand, OperatingCalendarDto>, SetOperatingDayCommandHandler>();
        services.AddScoped<IQueryHandler<PreviewOperatingDaysImportQuery, OperatingDaysImportDto>, PreviewOperatingDaysImportQueryHandler>();
        services.AddScoped<ICommandHandler<ApplyOperatingDaysImportCommand, OperatingDaysImportDto>, ApplyOperatingDaysImportCommandHandler>();
        services.AddScoped<ICommandHandler<CreateBlockingCommand, BlockingDto>, CreateBlockingCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteBlockingCommand, bool>, DeleteBlockingCommandHandler>();
        services.AddScoped<IQueryHandler<GetCablesQuery, IReadOnlyList<CableDetailDto>>, GetCablesQueryHandler>();
        services.AddScoped<ICommandHandler<ApplyOperationsSetupCommand, OperationsSetupResult>, ApplyOperationsSetupCommandHandler>();

        return services;
    }
}
