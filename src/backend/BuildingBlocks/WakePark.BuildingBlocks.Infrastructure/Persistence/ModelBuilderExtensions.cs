using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace WakePark.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Konvence platné pro všechny moduly. Drží schéma konzistentní bez toho,
/// aby to každá konfigurace entity opakovala.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Nastaví přesnost peněžních sloupců a vynutí <c>timestamptz</c> u všech okamžiků.
    /// </summary>
    public static ModelBuilder ApplyWakeParkConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(18);
                    property.SetScale(2);
                }

                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetColumnType("timestamptz");
                }
            }
        }

        return modelBuilder;
    }
}
