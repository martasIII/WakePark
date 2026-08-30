using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using WakePark.BuildingBlocks.Infrastructure.Persistence;

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence;

/// <summary>
/// Umožňuje spouštět <c>dotnet ef</c> nad tímto modulem bez startu celého hostu.
/// Connection string je jen návrhový — migrace se generují offline, na databázi se nesahá.
/// </summary>
public sealed class TenancyDbContextFactory : IDesignTimeDbContextFactory<TenancyDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=wakepark;Username=wakepark;Password=wakepark";

    public TenancyDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("WAKEPARK_DESIGNTIME_CONNECTION") ?? DesignTimeConnectionString;

        DbContextOptions<TenancyDbContext> options =
            (DbContextOptions<TenancyDbContext>)new DbContextOptionsBuilder<TenancyDbContext>()
                .UseWakeParkNpgsql(connectionString, TenancyDbContext.Schema)
                .Options;

        return new TenancyDbContext(options);
    }
}
