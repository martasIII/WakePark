using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Operations.Domain.Cables;

namespace WakePark.Modules.Operations.Infrastructure.Persistence.Configurations;

internal sealed class CableConfiguration : IEntityTypeConfiguration<Cable>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Cable> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("cables");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);

        // Schéma překážek je jen popis pro vizualizaci a nikdo se na něj neptá dotazem —
        // jeden jsonb sloupec, ne tabulka.
        builder.Ignore(c => c.Obstacles);
        builder.Property<List<Obstacle>>("_obstacles")
            .HasColumnName("obstacles")
            .HasColumnType("jsonb")
            .HasConversion(
                obstacles => JsonSerializer.Serialize(obstacles, Json),
                json => JsonSerializer.Deserialize<List<Obstacle>>(json, Json) ?? new List<Obstacle>(),
                new ValueComparer<List<Obstacle>>(
                    (a, b) => a!.SequenceEqual(b!),
                    obstacles => obstacles.Aggregate(0, (hash, o) => HashCode.Combine(hash, o.GetHashCode())),
                    obstacles => obstacles.ToList()))
            .IsRequired();

        builder.HasIndex(c => new { c.WakeParkId, c.Code }).IsUnique();

        builder.Ignore(c => c.DomainEvents);
    }
}
