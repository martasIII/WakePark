using Microsoft.EntityFrameworkCore;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Tenancy.Domain.Enterprises;
using WakePark.Modules.Tenancy.Domain.Parks;

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence;

/// <summary>
/// Databázový kontext modulu Tenancy. Modul má vlastní schéma a vlastní historii migrací —
/// tabulky jiných modulů odsud nejsou vidět.
/// </summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options)
    : DbContext(options)
{
    public const string Schema = "tenancy";

    public DbSet<Enterprise> Enterprises => Set<Enterprise>();

    public DbSet<Park> Parks => Set<Park>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);
        modelBuilder.ApplyWakeParkConventions();

        base.OnModelCreating(modelBuilder);
    }
}
