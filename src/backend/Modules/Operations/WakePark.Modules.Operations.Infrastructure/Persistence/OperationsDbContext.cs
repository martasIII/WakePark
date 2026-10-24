using Microsoft.EntityFrameworkCore;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Operations.Application.Abstractions;
using WakePark.Modules.Operations.Domain.Cables;
using WakePark.Modules.Operations.Domain.Calendar;
using WakePark.Modules.Operations.Domain.Parks;
using WakePark.Modules.Operations.Domain.Seasons;

namespace WakePark.Modules.Operations.Infrastructure.Persistence;

/// <summary>Databázový kontext modulu Operations. Vlastní schéma i historii migrací.</summary>
public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options)
    : DbContext(options), IOperationsUnitOfWork
{
    public const string Schema = "operations";

    public DbSet<Cable> Cables => Set<Cable>();

    public DbSet<Season> Seasons => Set<Season>();

    public DbSet<ParkSettings> ParkSettings => Set<ParkSettings>();

    public DbSet<OperatingDay> OperatingDays => Set<OperatingDay>();

    public DbSet<Blocking> Blockings => Set<Blocking>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsDbContext).Assembly);
        modelBuilder.ApplyWakeParkConventions();

        base.OnModelCreating(modelBuilder);
    }
}
