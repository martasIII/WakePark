using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Promotions;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("promotions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasConversion(new LocalizedTextConverter())
            .HasColumnType(LocalizedTextConverter.ColumnType)
            .IsRequired();

        // Výjimky pro produkty se čtou jen s akcí a je jich pár — jeden jsonb sloupec, ne tabulka.
        // Smazat produkt nejde, takže cizí klíč nechybí.
        builder.Ignore(p => p.ProductDiscounts);
        builder.Property<List<ProductDiscount>>("_productDiscounts")
            .HasColumnName("product_discounts")
            .HasColumnType("jsonb")
            .HasConversion(
                discounts => JsonSerializer.Serialize(discounts, Json),
                json => JsonSerializer.Deserialize<List<ProductDiscount>>(json, Json) ?? new List<ProductDiscount>(),
                new ValueComparer<List<ProductDiscount>>(
                    (a, b) => a!.SequenceEqual(b!),
                    discounts => discounts.Aggregate(0, (hash, d) => HashCode.Combine(hash, d.GetHashCode())),
                    discounts => discounts.ToList()))
            .HasDefaultValueSql("'[]'::jsonb")
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
