using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using WakePark.BuildingBlocks.Infrastructure.Persistence;

namespace WakePark.Modules.Operations.Infrastructure.Persistence;

/// <summary>
/// Umožňuje spouštět <c>dotnet ef</c> nad tímto modulem bez startu celého hostu.
/// Výchozí connection string míří na lokální vývojovou databázi z docker-compose;
/// pro jiný cíl nastav proměnnou <c>WAKEPARK_DESIGNTIME_CONNECTION</c>.
/// </summary>
public sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=wakepark;Username=wakepark;Password=wakepark";

    public OperationsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("WAKEPARK_DESIGNTIME_CONNECTION") ?? DesignTimeConnectionString;

        DbContextOptions<OperationsDbContext> options =
            (DbContextOptions<OperationsDbContext>)new DbContextOptionsBuilder<OperationsDbContext>()
                .UseWakeParkNpgsql(connectionString, OperationsDbContext.Schema)
                .Options;

        return new OperationsDbContext(options);
    }
}
