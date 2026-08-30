using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Doplňuje auditní sloupce při ukládání. Díky tomu je aplikační kód nemusí řešit
/// a nemůže na ně zapomenout.
/// </summary>
public sealed class AuditInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = clock.UtcNow;
        string? actor = currentUser.UserAccountId?.ToString();

        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedBy = actor;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedBy = actor;
                    break;

                default:
                    break;
            }
        }

        foreach (EntityEntry<ISoftDeletable> entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State is not EntityState.Deleted)
            {
                continue;
            }

            // Fyzické mazání převedeme na logické.
            entry.State = EntityState.Modified;
            entry.Entity.DeletedAtUtc = now;
            entry.Entity.DeletedBy = actor;
        }
    }
}
