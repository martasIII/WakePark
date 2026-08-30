using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Tenancy.Domain.Enterprises;

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence.Configurations;

internal sealed class EnterpriseConfiguration : IEntityTypeConfiguration<Enterprise>
{
    public void Configure(EntityTypeBuilder<Enterprise> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("enterprises");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RegistrationNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.VatNumber).HasMaxLength(20);
        builder.Property(e => e.BankAccountNumber).HasMaxLength(50);
        builder.Property(e => e.Iban).HasMaxLength(34);

        builder.OwnsOne(e => e.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(200).IsRequired();
            address.Property(a => a.HouseNumber).HasMaxLength(20);
            address.Property(a => a.City).HasMaxLength(100).IsRequired();
            address.Property(a => a.PostalCode).HasMaxLength(10).IsRequired();
            address.Property(a => a.CountryCode).HasMaxLength(2).IsRequired();
        });

        builder.HasIndex(e => e.RegistrationNumber).IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}
