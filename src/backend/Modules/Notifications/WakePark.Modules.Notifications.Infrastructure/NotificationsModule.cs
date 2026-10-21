using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Notifications.Application;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Infrastructure.Email;
using WakePark.Modules.Notifications.Infrastructure.Persistence;
using WakePark.Modules.Notifications.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Notifications.Infrastructure;

/// <summary>Skládací bod modulu Notifications.</summary>
public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("WakeParkDb")
            ?? throw new InvalidOperationException(
                "Chybí connection string 'WakeParkDb'. Doplň ho do appsettings nebo do proměnné prostředí.");

        services.AddDbContext<NotificationsDbContext>((serviceProvider, options) =>
            options
                .UseWakeParkNpgsql(connectionString, NotificationsDbContext.Schema)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

        NotificationsOptions settings =
            configuration.GetSection(NotificationsOptions.SectionName).Get<NotificationsOptions>() ?? new NotificationsOptions();

        services.AddSingleton(settings);
        services.AddScoped<INotificationsUnitOfWork>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<IEmailRepository, EmailRepository>();
        services.AddScoped<IEmailSender, MailKitEmailSender>();
        services.AddSingleton<IQrCodeImage, QrCodeImage>();
        services.AddNotificationsApplication();

        // E-maily se připravují vždy; odesílají se, jen když je to výslovně zapnuté.
        if (settings.Enabled)
        {
            services.AddHostedService<EmailSenderWorker>();
        }

        return services;
    }
}
