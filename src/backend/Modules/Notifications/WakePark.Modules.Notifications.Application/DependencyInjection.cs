using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application.Emails;
using WakePark.Modules.Notifications.Application.Orders;

namespace WakePark.Modules.Notifications.Application;

/// <summary>Registrace aplikační vrstvy modulu Notifications.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IIntegrationEventConsumer<OrderPlacedIntegrationEvent>, OrderPlacedConsumer>();
        services.AddScoped<ICommandHandler<SendPendingEmailsCommand, EmailBatchResult>, SendPendingEmailsCommandHandler>();

        return services;
    }
}
