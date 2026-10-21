using Microsoft.EntityFrameworkCore;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Billing.Domain.Payments;

namespace WakePark.Modules.Billing.Infrastructure.Persistence;

/// <summary>Databázový kontext modulu Billing — vlastní schéma, vlastní historie migrací.</summary>
public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options)
    : DbContext(options), IBillingUnitOfWork
{
    public const string Schema = "billing";

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();

    internal DbSet<OrderNumberSeries> OrderNumberSeries => Set<OrderNumberSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        modelBuilder.ApplyWakeParkConventions();

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
/// Čítač číselné řady objednávek — jeden řádek na provozovatele a rok. Není to doménová
/// entita, jen technika přidělování čísel (viz <see cref="Repositories.OrderNumberAllocator"/>).
/// </summary>
internal sealed class OrderNumberSeries
{
    public Guid EnterpriseId { get; set; }

    public int Year { get; set; }

    /// <summary>Poslední přidělené pořadí.</summary>
    public long LastSequence { get; set; }
}
