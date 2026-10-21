using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Vouchers.Domain;

namespace WakePark.Modules.Vouchers.Application.Abstractions;

public interface IVoucherRepository
{
    Task<bool> AnyForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Voucher>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    void Add(Voucher voucher);
}

/// <summary>
/// Zveřejnění integrační události přes outbox modulu Vouchers — uloží se spolu s ostatními
/// změnami při <see cref="IUnitOfWork.SaveChangesAsync"/>, v jedné transakci.
/// </summary>
public interface IVouchersOutbox
{
    void Publish(IIntegrationEvent integrationEvent);
}

/// <inheritdoc cref="IUnitOfWork" />
public interface IVouchersUnitOfWork : IUnitOfWork;
