using Microsoft.EntityFrameworkCore;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Domain.Abstractions;
using WakePark.BuildingBlocks.Infrastructure.Outbox;
using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Domain.Invoices;
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

    public DbSet<Invoice> Invoices => Set<Invoice>();

    internal DbSet<InvoiceNumberSeries> InvoiceNumberSeries => Set<InvoiceNumberSeries>();

    internal DbSet<OrderNumberSeries> OrderNumberSeries => Set<OrderNumberSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.ApplyWakeParkConventions();

        base.OnModelCreating(modelBuilder);
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await work(cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Události objednávek jdou do outboxu ve stejné transakci jako objednávka samotná.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Outbox.Collect(this, ToIntegrationEvent);
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Které doménové události Billing zveřejňuje ostatním modulům.</summary>
    private static IIntegrationEvent? ToIntegrationEvent(IDomainEvent domainEvent) => domainEvent switch
    {
        OrderPlaced e => new OrderPlacedIntegrationEvent(e.EventId, e.OccurredAtUtc, e.EnterpriseId, e.OrderId, e.AccessToken),
        OrderPaid e => new OrderPaidIntegrationEvent(e.EventId, e.OccurredAtUtc, e.EnterpriseId, e.OrderId, e.AccessToken),
        _ => null,
    };
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

/// <summary>Čítač řady daňových dokladů — jeden řádek na provozovatele a rok.</summary>
internal sealed class InvoiceNumberSeries
{
    public Guid EnterpriseId { get; set; }

    public int Year { get; set; }

    public long LastSequence { get; set; }
}
