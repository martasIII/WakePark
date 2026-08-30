using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Reservations.Application.Abstractions;
using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Reservations.Domain.Passes;
using WakePark.Modules.Reservations.Domain.Reservations;
using WakePark.Modules.Reservations.Domain.Slots;

namespace WakePark.Modules.Reservations.Infrastructure.Persistence;

/// <summary>
/// Databázový kontext modulu Reservations. Slot, rezervace i pohyby kreditu jsou schválně
/// v jednom kontextu — invariant „obsazeno ≤ kapacita" a odečet kreditu tak proběhnou
/// v jedné transakci.
/// </summary>
public sealed class ReservationsDbContext(DbContextOptions<ReservationsDbContext> options)
    : DbContext(options), IReservationsUnitOfWork
{
    public const string Schema = "reservations";

    public DbSet<Slot> Slots => Set<Slot>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<RidePass> RidePasses => Set<RidePass>();

    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReservationsDbContext).Assembly);
        modelBuilder.ApplyWakeParkConventions();

        base.OnModelCreating(modelBuilder);
    }
}
