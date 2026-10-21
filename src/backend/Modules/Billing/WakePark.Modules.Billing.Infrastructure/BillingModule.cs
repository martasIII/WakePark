using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Billing.Application;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Infrastructure.Fio;
using WakePark.Modules.Billing.Infrastructure.Persistence;
using WakePark.Modules.Billing.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Billing.Infrastructure;

/// <summary>Skládací bod modulu Billing.</summary>
public static class BillingModule
{
    public static IServiceCollection AddBillingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("WakeParkDb")
            ?? throw new InvalidOperationException(
                "Chybí connection string 'WakeParkDb'. Doplň ho do appsettings nebo do proměnné prostředí.");

        services.AddDbContext<BillingDbContext>((serviceProvider, options) =>
            options
                .UseWakeParkNpgsql(connectionString, BillingDbContext.Schema)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

        services.AddSingleton(configuration.GetSection(BillingOptions.SectionName).Get<BillingOptions>() ?? new BillingOptions());
        services.AddScoped<IBillingUnitOfWork>(sp => sp.GetRequiredService<BillingDbContext>());
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderNumberAllocator, OrderNumberAllocator>();
        services.AddScoped<IBankTransactionRepository, BankTransactionRepository>();

        // Fio: token je v adrese dotazu, proto HttpClient bez logování (jinak by ho vypsal do logu).
        IConfigurationSection fio = configuration.GetSection(FioOptions.SectionName);
        services.Configure<FioOptions>(fio);
        services.AddHttpClient<IBankStatementSource, FioBankStatementSource>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .RemoveAllLoggers();

        if (fio.Get<FioOptions>() is { IsEnabled: true })
        {
            services.AddHostedService<BankSyncWorker>();
        }

        services.AddBillingApplication();

        return services;
    }
}
