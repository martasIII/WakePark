using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Domain.Payments;

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Repositories;

internal sealed class BankTransactionRepository(BillingDbContext dbContext) : IBankTransactionRepository
{
    public async Task<IReadOnlySet<string>> GetKnownExternalIdsAsync(
        Guid enterpriseId,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken = default)
    {
        if (externalIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        List<string> known = await dbContext.BankTransactions
            .AsNoTracking()
            .Where(t => t.EnterpriseId == enterpriseId && externalIds.Contains(t.ExternalId))
            .Select(t => t.ExternalId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return known.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<BankTransaction>> GetReceivedAsync(
        Guid enterpriseId,
        CancellationToken cancellationToken = default) =>
        await dbContext.BankTransactions
            .Where(t => t.EnterpriseId == enterpriseId && t.Status == BankTransactionStatus.Received)
            .OrderBy(t => t.BookedOn)
            .ThenBy(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(BankTransaction transaction) => dbContext.BankTransactions.Add(transaction);
}
