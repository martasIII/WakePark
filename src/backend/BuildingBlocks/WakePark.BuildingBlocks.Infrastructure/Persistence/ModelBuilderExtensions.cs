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
    /// Označí primární klíče jako přiřazované aplikací, nastaví přesnost peněžních sloupců
    /// a vynutí <c>timestamptz</c> u všech okamžiků.
    /// </summary>
    public static ModelBuilder ApplyWakeParkConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Primární klíče přiřazujeme v kódu (UUID v7), nikdy je negeneruje databáze.
            // Bez tohoto by EF u Guid klíčů předpokládal generování na straně databáze a nový
            // objekt v agregátu — který klíč už má — by považoval za existující řádek
            // a místo INSERT vydal UPDATE, který nic nezmění.
            foreach (IMutableProperty keyProperty in entityType.FindPrimaryKey()?.Properties ?? [])
            {
                if (keyProperty.ClrType == typeof(Guid))
                {
                    keyProperty.ValueGenerated = ValueGenerated.Never;
                }
            }

            foreach (IMutableProperty property in entityType.GetProperties())
            {
                // Výchozí přesnost pro peníze; výslovné HasPrecision (procenta, GPS) má přednost.
                if ((property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?)) && property.GetPrecision() is null)
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
