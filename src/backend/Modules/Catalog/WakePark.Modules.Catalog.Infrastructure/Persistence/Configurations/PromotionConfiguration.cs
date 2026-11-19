using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Promotions;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("promotions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasConversion(new LocalizedTextConverter())
            .HasColumnType(LocalizedTextConverter.ColumnType)
            .IsRequired();

        // Kategorii s akcí smazat nejde (hlídá aplikace), proto Restrict.
        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.EnterpriseId, p.ValidTo });

        builder.Ignore(p => p.DomainEvents);
    }
}
