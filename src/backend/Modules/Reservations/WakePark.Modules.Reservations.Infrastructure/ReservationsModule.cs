using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Reservations.Application;
using WakePark.Modules.Reservations.Application.Abstractions;
using WakePark.Modules.Reservations.Infrastructure.Operations;
using WakePark.Modules.Reservations.Infrastructure.Persistence;
using WakePark.Modules.Reservations.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Reservations.Infrastructure;

/// <summary>Skládací bod modulu Reservations.</summary>
public static class ReservationsModule
{
    public static IServiceCollection AddReservationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("WakeParkDb")
            ?? throw new InvalidOperationException(
                "Chybí connection string 'WakeParkDb'. Doplň ho do appsettings nebo do proměnné prostředí.");

        services.AddDbContext<ReservationsDbContext>((serviceProvider, options) =>
            options
                .UseWakeParkNpgsql(connectionString, ReservationsDbContext.Schema)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<IReservationsUnitOfWork>(sp => sp.GetRequiredService<ReservationsDbContext>());
        services.AddScoped<IReservationsRepository, ReservationsRepository>();
        services.AddScoped<ICableLocator, CableLocator>();
        services.AddReservationsApplication();

        return services;
    }
}
