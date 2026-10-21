using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Catalog.Application;
using WakePark.Modules.Catalog.Application.Abstractions;
using WakePark.Modules.Catalog.Infrastructure.Persistence;
using WakePark.Modules.Catalog.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Catalog.Infrastructure;

/// <summary>Skládací bod modulu Catalog.</summary>
public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("WakeParkDb")
            ?? throw new InvalidOperationException(
                "Chybí connection string 'WakeParkDb'. Doplň ho do appsettings nebo do proměnné prostředí.");

        services.AddDbContext<CatalogDbContext>((serviceProvider, options) =>
            options
                .UseWakeParkNpgsql(connectionString, CatalogDbContext.Schema)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<ICatalogUnitOfWork>(sp => sp.GetRequiredService<CatalogDbContext>());
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICatalogReadRepository, CatalogReadRepository>();
        services.AddCatalogApplication();

        return services;
    }
}
