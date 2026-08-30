using Microsoft.EntityFrameworkCore;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Identity.Application.Abstractions;
using WakePark.Modules.Identity.Domain.Authorization;
using WakePark.Modules.Identity.Domain.Users;

namespace WakePark.Modules.Identity.Infrastructure.Persistence;

/// <summary>Databázový kontext modulu Identity. Vlastní schéma i historii migrací.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IIdentityUnitOfWork
{
    public const string Schema = "identity";

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserParkRole> UserParkRoles => Set<UserParkRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplyWakeParkConventions();

        base.OnModelCreating(modelBuilder);
    }
}
