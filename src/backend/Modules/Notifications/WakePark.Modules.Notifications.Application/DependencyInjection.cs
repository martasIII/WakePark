using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application.Admin;
using WakePark.Modules.Notifications.Application.Emails;
using WakePark.Modules.Notifications.Application.Orders;
using WakePark.Modules.Vouchers.Contracts;

namespace WakePark.Modules.Notifications.Application;

/// <summary>Registrace aplikační vrstvy modulu Notifications.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<OrderPlacedConsumer>();
        services.AddScoped<IIntegrationEventConsumer<OrderPlacedIntegrationEvent>>(sp => sp.GetRequiredService<OrderPlacedConsumer>());
        services.AddScoped<IIntegrationEventConsumer<OrderPaymentRemindedIntegrationEvent>>(sp => sp.GetRequiredService<OrderPlacedConsumer>());
        services.AddScoped<IIntegrationEventConsumer<OrderCancelledIntegrationEvent>, OrderCancelledConsumer>();
        services.AddScoped<IIntegrationEventConsumer<VouchersIssuedIntegrationEvent>, VouchersIssuedConsumer>();
        services.AddScoped<ICommandHandler<SendPendingEmailsCommand, EmailBatchResult>, SendPendingEmailsCommandHandler>();
        services.AddScoped<IIntegrationEventConsumer<PaymentNeedsAttentionIntegrationEvent>, PaymentNeedsAttentionConsumer>();
        services.AddScoped<IAdminAlerts, AdminAlerts>();

        return services;
    }
}
