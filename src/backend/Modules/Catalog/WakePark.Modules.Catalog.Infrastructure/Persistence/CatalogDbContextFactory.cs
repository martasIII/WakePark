using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using WakePark.BuildingBlocks.Infrastructure.Persistence;

namespace WakePark.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// Umožňuje spouštět <c>dotnet ef</c> nad tímto modulem bez startu celého hostu.
/// Connection string je jen návrhový — migrace se generují offline, na databázi se nesahá.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=wakepark;Username=wakepark;Password=wakepark";

    public CatalogDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("WAKEPARK_DESIGNTIME_CONNECTION") ?? DesignTimeConnectionString;

        DbContextOptions<CatalogDbContext> options =
            (DbContextOptions<CatalogDbContext>)new DbContextOptionsBuilder<CatalogDbContext>()
                .UseWakeParkNpgsql(connectionString, CatalogDbContext.Schema)
                .Options;

        return new CatalogDbContext(options);
    }
}
