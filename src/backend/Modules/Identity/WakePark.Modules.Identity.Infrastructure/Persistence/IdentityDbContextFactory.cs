using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using WakePark.BuildingBlocks.Infrastructure.Persistence;

namespace WakePark.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Umožňuje spouštět <c>dotnet ef</c> nad tímto modulem bez startu celého hostu.
/// Výchozí connection string míří na lokální vývojovou databázi z docker-compose;
/// pro jiný cíl nastav proměnnou <c>WAKEPARK_DESIGNTIME_CONNECTION</c>.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=wakepark;Username=wakepark;Password=wakepark";

    public IdentityDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("WAKEPARK_DESIGNTIME_CONNECTION") ?? DesignTimeConnectionString;

        DbContextOptions<IdentityDbContext> options =
            (DbContextOptions<IdentityDbContext>)new DbContextOptionsBuilder<IdentityDbContext>()
                .UseWakeParkNpgsql(connectionString, IdentityDbContext.Schema)
                .Options;

        return new IdentityDbContext(options);
    }
}
