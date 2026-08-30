using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Identity.Application;
using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Infrastructure.Persistence;
using WakePark.Modules.Identity.Infrastructure.Persistence.Repositories;
using WakePark.Modules.Identity.Infrastructure.Security;

namespace WakePark.Modules.Identity.Infrastructure;

/// <summary>Skládací bod modulu Identity.</summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("WakeParkDb")
            ?? throw new InvalidOperationException(
                "Chybí connection string 'WakeParkDb'. Doplň ho do appsettings nebo do proměnné prostředí.");

        services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
            options
                .UseWakeParkNpgsql(connectionString, IdentityDbContext.Schema)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<IIdentityUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IUserAccessReader, UserAccessReader>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddScoped<IdentitySeeder>();
        services.AddIdentityApplication();

        return services;
    }
}
