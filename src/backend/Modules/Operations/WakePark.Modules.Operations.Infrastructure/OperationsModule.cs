using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Operations.Application;
using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Infrastructure.Persistence;
using WakePark.Modules.Operations.Infrastructure.Persistence.Repositories;
using WakePark.Modules.Operations.Infrastructure.Tenancy;

namespace WakePark.Modules.Operations.Infrastructure;

/// <summary>Skládací bod modulu Operations.</summary>
public static class OperationsModule
{
    public static IServiceCollection AddOperationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("WakeParkDb")
            ?? throw new InvalidOperationException(
                "Chybí connection string 'WakeParkDb'. Doplň ho do appsettings nebo do proměnné prostředí.");

        services.AddDbContext<OperationsDbContext>((serviceProvider, options) =>
            options
                .UseWakeParkNpgsql(connectionString, OperationsDbContext.Schema)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

        services.AddMemoryCache();
        services.AddScoped<IOperationsRepository, OperationsRepository>();
        services.AddScoped<IParkTimeZoneProvider, ParkTimeZoneProvider>();
        services.AddOperationsApplication();

        return services;
    }
}
