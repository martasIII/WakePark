using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("product_categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(CatalogCode.MaxLength).IsRequired();
        builder.Property(c => c.Name)
            .HasConversion(new LocalizedTextConverter())
            .HasColumnType(LocalizedTextConverter.ColumnType)
            .IsRequired();

        builder.HasIndex(c => new { c.EnterpriseId, c.Code }).IsUnique();

        builder.Ignore(c => c.DomainEvents);
    }
}
