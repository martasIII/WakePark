using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Invoices;
using WakePark.Modules.Billing.Application.Orders;
using WakePark.Modules.Billing.Application.Payments;
using WakePark.Modules.Billing.Contracts;

namespace WakePark.Modules.Billing.Application;

/// <summary>Registrace aplikační vrstvy modulu Billing.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddBillingApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommandHandler<PlaceOrderCommand, PlacedOrderDto>, PlaceOrderCommandHandler>();
        services.AddScoped<IQueryHandler<GetOrderQuery, OrderDto?>, GetOrderQueryHandler>();
        services.AddScoped<IOrderDirectory, OrderDirectory>();
        services.AddScoped<ICommandHandler<ProcessUnpaidOrdersCommand, UnpaidOrdersResult>, ProcessUnpaidOrdersCommandHandler>();
        services.AddScoped<IInvoiceDirectory, InvoiceDirectory>();
        services.AddScoped<ICommandHandler<SyncBankTransactionsCommand, BankSyncResult>, SyncBankTransactionsCommandHandler>();

        return services;
    }
}
