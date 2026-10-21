using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using WakePark.BuildingBlocks.Infrastructure.Persistence;

namespace WakePark.Modules.Billing.Infrastructure.Persistence;

/// <summary>
/// Umožňuje spouštět <c>dotnet ef</c> nad tímto modulem bez startu celého hostu.
/// Connection string je jen návrhový — migrace se generují offline, na databázi se nesahá.
/// </summary>
public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=wakepark;Username=wakepark;Password=wakepark";

    public BillingDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("WAKEPARK_DESIGNTIME_CONNECTION") ?? DesignTimeConnectionString;

        DbContextOptions<BillingDbContext> options =
            (DbContextOptions<BillingDbContext>)new DbContextOptionsBuilder<BillingDbContext>()
                .UseWakeParkNpgsql(connectionString, BillingDbContext.Schema)
                .Options;

        return new BillingDbContext(options);
    }
}
