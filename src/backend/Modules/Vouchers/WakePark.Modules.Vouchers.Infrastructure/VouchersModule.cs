using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Infrastructure.Outbox;
using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Vouchers.Application;
using WakePark.Modules.Vouchers.Application.Abstractions;
using WakePark.Modules.Vouchers.Infrastructure.Persistence;
using WakePark.Modules.Vouchers.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Vouchers.Infrastructure;

/// <summary>Skládací bod modulu Vouchers.</summary>
public static class VouchersModule
{
    public static IServiceCollection AddVouchersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("WakeParkDb")
            ?? throw new InvalidOperationException(
                "Chybí connection string 'WakeParkDb'. Doplň ho do appsettings nebo do proměnné prostředí.");

        services.AddDbContext<VouchersDbContext>((serviceProvider, options) =>
            options
                .UseWakeParkNpgsql(connectionString, VouchersDbContext.Schema)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

        services.AddSingleton(configuration.GetSection(VouchersOptions.SectionName).Get<VouchersOptions>() ?? new VouchersOptions());
        services.AddScoped<IVouchersUnitOfWork>(sp => sp.GetRequiredService<VouchersDbContext>());
        services.AddScoped<IVouchersOutbox>(sp => sp.GetRequiredService<VouchersDbContext>());
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddVouchersApplication();

        services.AddSingleton<OutboxDispatcher<VouchersDbContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxDispatcher<VouchersDbContext>>());

        return services;
    }
}
