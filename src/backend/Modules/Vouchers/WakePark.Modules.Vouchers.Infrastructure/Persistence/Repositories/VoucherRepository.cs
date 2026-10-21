using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Vouchers.Application.Abstractions;
using WakePark.Modules.Vouchers.Domain;

namespace WakePark.Modules.Vouchers.Infrastructure.Persistence.Repositories;

internal sealed class VoucherRepository(VouchersDbContext dbContext) : IVoucherRepository
{
    public Task<bool> AnyForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        dbContext.Vouchers.AnyAsync(v => v.OrderId == orderId, cancellationToken);

    public async Task<IReadOnlyList<Voucher>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await dbContext.Vouchers
            .AsNoTracking()
            .Where(v => v.OrderId == orderId)
            .OrderBy(v => v.ProductCode)
            .ThenBy(v => v.Piece)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Voucher voucher) => dbContext.Vouchers.Add(voucher);
}
