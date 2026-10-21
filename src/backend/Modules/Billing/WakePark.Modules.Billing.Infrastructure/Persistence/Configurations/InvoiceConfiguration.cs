using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Billing.Domain.Invoices;

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("invoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Number).HasMaxLength(20).IsRequired();
        builder.Property(i => i.VariableSymbol).HasMaxLength(10).IsRequired();
        builder.Property(i => i.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        builder.OwnsOne(i => i.Seller, Party);
        builder.OwnsOne(i => i.Buyer, Party);
        builder.Navigation(i => i.Seller).IsRequired();
        builder.Navigation(i => i.Buyer).IsRequired();

        builder.OwnsMany(i => i.Lines, line =>
        {
            line.ToTable("invoice_lines");
            line.WithOwner().HasForeignKey("InvoiceId");
            line.Property<int>("Id");
            line.HasKey("Id");
            line.Property(l => l.Description).HasMaxLength(500).IsRequired();
        });

        builder.Navigation(i => i.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Řada bez mezer a nejvýš jeden doklad k objednávce.
        builder.HasIndex(i => new { i.EnterpriseId, i.Number }).IsUnique();
        builder.HasIndex(i => i.OrderId).IsUnique();

        builder.Ignore(i => i.DomainEvents);
    }

    private static void Party<TOwner>(OwnedNavigationBuilder<TOwner, InvoiceParty> party)
        where TOwner : class
    {
        party.Property(p => p.Name).HasMaxLength(200).IsRequired();
        party.Property(p => p.CompanyId).HasMaxLength(8);
        party.Property(p => p.VatId).HasMaxLength(12);
        party.Property(p => p.Address).HasMaxLength(300);
        party.Property(p => p.Registration).HasMaxLength(200);
        party.Property(p => p.ContactName).HasMaxLength(200);
        party.Property(p => p.Email).HasMaxLength(254);
    }
}

internal sealed class InvoiceNumberSeriesConfiguration : IEntityTypeConfiguration<InvoiceNumberSeries>
{
    public void Configure(EntityTypeBuilder<InvoiceNumberSeries> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("invoice_number_series");
        builder.HasKey(s => new { s.EnterpriseId, s.Year });
    }
}
