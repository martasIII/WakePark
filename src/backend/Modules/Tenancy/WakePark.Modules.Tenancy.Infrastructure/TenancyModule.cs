using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Tenancy.Application;
using WakePark.Modules.Tenancy.Application.Abstractions;
using WakePark.Modules.Tenancy.Infrastructure.Persistence;
using WakePark.Modules.Tenancy.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Tenancy.Infrastructure;

/// <summary>
/// Skládací bod modulu. Host o vnitřku modulu nic neví — zavolá jen tuhle metodu.
/// </summary>
public static class TenancyModule
{
    public static IServiceCollection AddTenancyModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("WakeParkDb")
            ?? throw new InvalidOperationException(
                "Chybí connection string 'WakeParkDb'. Doplň ho do appsettings nebo do proměnné prostředí.");

        services.AddDbContext<TenancyDbContext>((serviceProvider, options) =>
            options
                .UseWakeParkNpgsql(connectionString, TenancyDbContext.Schema)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TenancyDbContext>());
        services.AddScoped<IParkReadRepository, ParkReadRepository>();
        services.AddTenancyApplication();

        return services;
    }
}
