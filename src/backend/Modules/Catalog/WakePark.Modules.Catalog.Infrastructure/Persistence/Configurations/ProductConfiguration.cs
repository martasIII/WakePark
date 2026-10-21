using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Common;
using WakePark.Modules.Catalog.Domain.Products;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code).HasMaxLength(CatalogCode.MaxLength).IsRequired();
        builder.Property(p => p.ImagePath).HasMaxLength(Product.ImagePathMaxLength);

        builder.Property(p => p.Name)
            .HasConversion(new LocalizedTextConverter())
            .HasColumnType(LocalizedTextConverter.ColumnType)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasConversion(new LocalizedTextConverter())
            .HasColumnType(LocalizedTextConverter.ColumnType)
            .IsRequired();

        // Kategorie je ve stejném modulu, takže cizí klíč smí být skutečný.
        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.EnterpriseId, p.Code }).IsUnique();

        builder.Ignore(p => p.DomainEvents);
    }
}
