using WakePark.BuildingBlocks.Application.Messaging;

namespace WakePark.Modules.Billing.Contracts;

/// <summary>
/// Objednávka vznikla — Notifications pošle potvrzení s platebními údaji. Událost je „tenká“:
/// podrobnosti si příjemce přečte přes <see cref="IOrderDirectory"/>, aby se v outboxu
/// neukládaly osobní údaje navíc.
/// </summary>
public sealed record OrderPlacedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAtUtc, Guid EnterpriseId, Guid OrderId, string AccessToken)
    : IIntegrationEvent;

/// <summary>Objednávka je zaplacená — vystaví se voucher a doklad.</summary>
public sealed record OrderPaidIntegrationEvent(Guid EventId, DateTimeOffset OccurredAtUtc, Guid EnterpriseId, Guid OrderId, string AccessToken)
    : IIntegrationEvent;

/// <summary>Daňový doklad k objednávce pro ostatní moduly.</summary>
public interface IInvoiceDirectory
{
    Task<InvoiceDto?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}

/// <summary>Čtení objednávky pro ostatní moduly.</summary>
public interface IOrderDirectory
{
    Task<OrderDto?> GetOrderAsync(string accessToken, CancellationToken cancellationToken = default);
}
