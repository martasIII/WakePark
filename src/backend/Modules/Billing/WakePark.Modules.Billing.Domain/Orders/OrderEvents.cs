using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Billing.Domain.Orders;

/// <summary>Objednávka vznikla a čeká na platbu.</summary>
public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAtUtc, Guid OrderId, string AccessToken) : IDomainEvent;

/// <summary>Platba za objednávku dorazila.</summary>
public sealed record OrderPaid(Guid EventId, DateTimeOffset OccurredAtUtc, Guid OrderId, string AccessToken) : IDomainEvent;
