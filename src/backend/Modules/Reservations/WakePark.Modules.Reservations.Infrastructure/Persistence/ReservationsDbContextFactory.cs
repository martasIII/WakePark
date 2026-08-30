using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using WakePark.BuildingBlocks.Infrastructure.Persistence;

namespace WakePark.Modules.Reservations.Infrastructure.Persistence;

/// <summary>
/// Umožňuje spouštět <c>dotnet ef</c> nad tímto modulem bez startu celého hostu.
/// Výchozí connection string míří na lokální vývojovou databázi z docker-compose;
/// pro jiný cíl nastav proměnnou <c>WAKEPARK_DESIGNTIME_CONNECTION</c>.
/// </summary>
public sealed class ReservationsDbContextFactory : IDesignTimeDbContextFactory<ReservationsDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=wakepark;Username=wakepark;Password=wakepark";

    public ReservationsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("WAKEPARK_DESIGNTIME_CONNECTION") ?? DesignTimeConnectionString;

        DbContextOptions<ReservationsDbContext> options =
            (DbContextOptions<ReservationsDbContext>)new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseWakeParkNpgsql(connectionString, ReservationsDbContext.Schema)
                .Options;

        return new ReservationsDbContext(options);
    }
}
