using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Reservations.Domain.Passes;

namespace WakePark.Modules.Reservations.Infrastructure.Persistence.Configurations;

internal sealed class RidePassConfiguration : IEntityTypeConfiguration<RidePass>
{
    public void Configure(EntityTypeBuilder<RidePass> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ride_passes");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasMany(p => p.Transactions)
            .WithOne()
            .HasForeignKey(t => t.RidePassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Transactions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => new { p.CustomerId, p.WakeParkId });

        builder.Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        // Zůstatek je dopočet ze součtu pohybů, ne sloupec.
        builder.Ignore(p => p.Balance);
        builder.Ignore(p => p.ConsumesCredit);
        builder.Ignore(p => p.DomainEvents);
    }
}
