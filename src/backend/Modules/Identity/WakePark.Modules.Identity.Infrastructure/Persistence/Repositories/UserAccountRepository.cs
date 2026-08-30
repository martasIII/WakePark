using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Domain.Users;

namespace WakePark.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class UserAccountRepository(IdentityDbContext dbContext) : IUserAccountRepository
{
    public async Task<UserAccount?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserAccounts
            .FirstOrDefaultAsync(account => account.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

    public async Task<UserAccount?> GetByIdAsync(
        Guid userAccountId,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserAccounts
            .FirstOrDefaultAsync(account => account.Id == userAccountId, cancellationToken)
            .ConfigureAwait(false);

    public void Add(UserAccount account) => dbContext.UserAccounts.Add(account);
}
