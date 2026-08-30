using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Reservations.Domain.Reservations;

namespace WakePark.Modules.Reservations.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("reservations");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.CancellationReason).HasMaxLength(500);
        builder.Property(r => r.IdempotencyKey).HasMaxLength(64);
        builder.Property(r => r.RefundPercent).HasPrecision(5, 2);

        // Filtrovaný unikátní index: klíč je volitelný, ale pokud je zadaný, musí být jedinečný.
        builder.HasIndex(r => r.IdempotencyKey)
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL");

        builder.HasIndex(r => r.SlotId);
        builder.HasIndex(r => r.CustomerId);

        builder.Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.Ignore(r => r.DomainEvents);
    }
}
