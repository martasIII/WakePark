using WakePark.BuildingBlocks.Application.Messaging;

namespace WakePark.Modules.Vouchers.Contracts;

/// <summary>Poukazy k objednávce jsou vystavené — Notifications pošle e-mail s PDF.</summary>
public sealed record VouchersIssuedIntegrationEvent(Guid EventId, DateTimeOffset OccurredAtUtc, Guid EnterpriseId, Guid OrderId, string AccessToken)
    : IIntegrationEvent;

public sealed record VoucherDto(
    string Code,
    long OrderNumber,
    string ProductCode,
    string Name,
    string DisplayName,
    string Language,
    decimal Price,
    int Piece,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Status);

/// <summary>Čtení poukazů pro ostatní moduly.</summary>
public interface IVoucherDirectory
{
    Task<IReadOnlyList<VoucherDto>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
