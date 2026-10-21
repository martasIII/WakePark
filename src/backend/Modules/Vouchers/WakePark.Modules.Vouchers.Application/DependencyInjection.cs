using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Vouchers.Contracts;

namespace WakePark.Modules.Vouchers.Application;

/// <summary>Registrace aplikační vrstvy modulu Vouchers.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddVouchersApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IIntegrationEventConsumer<OrderPaidIntegrationEvent>, OrderPaidConsumer>();
        services.AddScoped<IVoucherDirectory, VoucherDirectory>();

        return services;
    }
}
