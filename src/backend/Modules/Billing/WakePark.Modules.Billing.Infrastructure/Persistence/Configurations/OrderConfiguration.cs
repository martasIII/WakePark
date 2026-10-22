using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Billing.Domain.Customers;
using WakePark.Modules.Billing.Domain.Orders;

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.AccessToken).HasMaxLength(Order.AccessTokenLength).IsFixedLength().IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Language).HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(o => o.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        // Kontakt je součást objednávky, ne samostatná tabulka — zamrazený k okamžiku objednání.
        builder.OwnsOne(o => o.Customer, customer =>
        {
            customer.Property(c => c.Name).HasMaxLength(CustomerContact.NameMaxLength).IsRequired();
            customer.Property(c => c.Email).HasMaxLength(CustomerContact.EmailMaxLength).IsRequired();
            customer.Property(c => c.Phone).HasMaxLength(CustomerContact.PhoneMaxLength);
            customer.Property(c => c.CompanyName).HasMaxLength(CustomerContact.NameMaxLength);
            customer.Property(c => c.CompanyId).HasMaxLength(8);
            customer.Property(c => c.VatId).HasMaxLength(12);
            customer.Property(c => c.CompanyAddress).HasMaxLength(CustomerContact.AddressMaxLength);
        });

        builder.Navigation(o => o.Customer).IsRequired();

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(o => o.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Číslo je variabilní symbol — podle něj se bude párovat platba z banky.
        builder.HasIndex(o => new { o.EnterpriseId, o.Number }).IsUnique();
        builder.HasIndex(o => o.AccessToken).IsUnique();
        builder.HasIndex(o => new { o.Status, o.PaymentDueAtUtc });

        builder.Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("order_lines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ProductCode).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(OrderLine.DescriptionMaxLength).IsRequired();
        builder.Property(l => l.DisplayName).HasMaxLength(OrderLine.DescriptionMaxLength).IsRequired();

        // Na produkt jen odkaz bez cizího klíče — produkt žije v jiném modulu (a jiném schématu).
        builder.HasIndex(l => l.ProductId);
    }
}

internal sealed class OrderNumberSeriesConfiguration : IEntityTypeConfiguration<OrderNumberSeries>
{
    public void Configure(EntityTypeBuilder<OrderNumberSeries> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("order_number_series");
        builder.HasKey(s => new { s.EnterpriseId, s.Year });
    }
}
