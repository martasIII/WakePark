using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    /// <summary>Politika omezení pokusů o přihlášení (viz <see cref="AddIdentityModule"/>).</summary>
    public const string SignInRateLimitPolicy = "identity.sign-in";

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

        // Druhá vrstva vedle zamykání účtu: zamčení chrání jeden účet, tohle zpomalí toho,
        // kdo z jedné adresy zkouší hesla k mnoha účtům. Host musí volat UseRateLimiter
        // až za UseForwardedHeaders, jinak by se počítalo podle IP nginxu.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(SignInRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = configuration.GetValue("Identity:SignInRateLimit:PermitLimit", 10),
                        Window = configuration.GetValue("Identity:SignInRateLimit:Window", TimeSpan.FromMinutes(10)),
                    }));
        });

        return services;
    }
}
