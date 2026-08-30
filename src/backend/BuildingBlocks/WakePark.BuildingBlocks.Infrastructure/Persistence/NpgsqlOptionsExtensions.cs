using Microsoft.EntityFrameworkCore;

namespace WakePark.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Jednotné nastavení Npgsql pro všechny moduly. Každý modul má vlastní schéma
/// i vlastní historii migrací, ale konvence musí být společné.
/// </summary>
public static class NpgsqlOptionsExtensions
{
    public static DbContextOptionsBuilder UseWakeParkNpgsql(
        this DbContextOptionsBuilder options,
        string connectionString,
        string schema)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        return options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            // V Postgresu je snake_case standard. Bez toho by se všechny sloupce musely
            // v psql psát v uvozovkách ("CreatedAtUtc"), protože Postgres jinak identifikátory
            // převádí na malá písmena.
            .UseSnakeCaseNamingConvention();
    }
}
