using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Catalog.Domain.Pricing;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("price_lists");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => i.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Zatím platí jeden aktivní ceník na provozovatele; import na to spoléhá.
        builder.HasIndex(p => p.EnterpriseId)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ix_price_lists_enterprise_id_active");

        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class PriceItemConfiguration : IEntityTypeConfiguration<PriceItem>
{
    public void Configure(EntityTypeBuilder<PriceItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("price_items");
        builder.HasKey(i => i.Id);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.PriceListId, i.ProductId }).IsUnique();
    }
}
