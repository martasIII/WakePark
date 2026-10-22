using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Billing.Domain.Payments;

/// <summary>Příchozí platba se nedala spárovat automaticky.</summary>
public sealed record PaymentNeedsAttention(Guid EventId, DateTimeOffset OccurredAtUtc, Guid EnterpriseId, Guid BankTransactionId) : IDomainEvent;
