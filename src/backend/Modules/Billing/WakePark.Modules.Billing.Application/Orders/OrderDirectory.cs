using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Contracts;

namespace WakePark.Modules.Billing.Application.Orders;

/// <summary>Objednávka pro ostatní moduly — stejný pohled jako pro web (<see cref="GetOrderQuery"/>).</summary>
internal sealed class OrderDirectory(IQueryHandler<GetOrderQuery, OrderDto?> query) : IOrderDirectory
{
    public Task<OrderDto?> GetOrderAsync(string accessToken, CancellationToken cancellationToken = default) =>
        query.HandleAsync(new GetOrderQuery(accessToken), cancellationToken);
}
