using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Infrastructure.Outbox;
using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Vouchers.Application.Abstractions;
using WakePark.Modules.Vouchers.Domain;

namespace WakePark.Modules.Vouchers.Infrastructure.Persistence;

/// <summary>Databázový kontext modulu Vouchers — poukazy a outbox.</summary>
public sealed class VouchersDbContext(DbContextOptions<VouchersDbContext> options)
    : DbContext(options), IVouchersUnitOfWork, IVouchersOutbox
{
    public const string Schema = "vouchers";

    public DbSet<Voucher> Vouchers => Set<Voucher>();

    public void Publish(IIntegrationEvent integrationEvent) =>
        Set<OutboxMessage>().Add(OutboxMessage.From(integrationEvent));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VouchersDbContext).Assembly);
        modelBuilder.AddOutbox();
        modelBuilder.ApplyWakeParkConventions();

        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("vouchers");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Code).HasMaxLength(Voucher.CodeMaxLength).IsRequired();
        builder.Property(v => v.ProductCode).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Name).HasMaxLength(500).IsRequired();
        builder.Property(v => v.DisplayName).HasMaxLength(500).IsRequired();
        builder.Property(v => v.Language).HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);

        // Kód je jedinečný napříč provozovatelem — podle něj se poukaz uplatní.
        builder.HasIndex(v => new { v.EnterpriseId, v.Code }).IsUnique();
        // Stejná objednávka nevyrobí stejný kus dvakrát, ani při souběžném doručení.
        builder.HasIndex(v => new { v.OrderId, v.ProductId, v.Piece }).IsUnique();

        builder.Ignore(v => v.DomainEvents);
    }
}
