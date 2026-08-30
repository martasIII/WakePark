using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Tenancy.Domain.Parks;

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence.Configurations;

internal sealed class ParkConfiguration : IEntityTypeConfiguration<Park>
{
    public void Configure(EntityTypeBuilder<Park> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("wake_parks");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(p => p.ReservationEmail).HasMaxLength(320);

        builder.OwnsOne(p => p.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(200).IsRequired();
            address.Property(a => a.HouseNumber).HasMaxLength(20);
            address.Property(a => a.City).HasMaxLength(100).IsRequired();
            address.Property(a => a.PostalCode).HasMaxLength(10).IsRequired();
            address.Property(a => a.CountryCode).HasMaxLength(2).IsRequired();
        });

        builder.OwnsOne(p => p.CancellationPolicy, policy =>
        {
            policy.Property(c => c.LateCancellationRefundPercent).HasPrecision(5, 2);
            policy.Property(c => c.NoShowRefundPercent).HasPrecision(5, 2);
        });

        builder.HasIndex(p => p.EnterpriseId);

        builder.Ignore(p => p.DomainEvents);
    }
}
