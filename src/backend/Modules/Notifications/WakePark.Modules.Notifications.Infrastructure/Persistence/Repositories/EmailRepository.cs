using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.Infrastructure.Persistence.Repositories;

internal sealed class EmailRepository(NotificationsDbContext dbContext) : IEmailRepository
{
    public Task<bool> ExistsAsync(Guid sourceEventId, string kind, CancellationToken cancellationToken = default) =>
        dbContext.Emails.AnyAsync(e => e.SourceEventId == sourceEventId && e.Kind == kind, cancellationToken);

    public async Task<IReadOnlyList<EmailMessage>> GetDueAsync(DateTimeOffset nowUtc, int max, CancellationToken cancellationToken = default) =>
        await dbContext.Emails
            .Where(e => e.Status == EmailStatus.Pending && (e.NextAttemptAtUtc == null || e.NextAttemptAtUtc <= nowUtc))
            .OrderBy(e => e.CreatedAtUtc)
            .Take(max)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(EmailMessage message) => dbContext.Emails.Add(message);
}
