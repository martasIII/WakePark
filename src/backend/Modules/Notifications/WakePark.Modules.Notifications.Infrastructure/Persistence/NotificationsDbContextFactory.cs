using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using WakePark.BuildingBlocks.Infrastructure.Persistence;

namespace WakePark.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Umožňuje spouštět <c>dotnet ef</c> nad tímto modulem bez startu celého hostu.
/// Connection string je jen návrhový — migrace se generují offline, na databázi se nesahá.
/// </summary>
public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=wakepark;Username=wakepark;Password=wakepark";

    public NotificationsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("WAKEPARK_DESIGNTIME_CONNECTION") ?? DesignTimeConnectionString;

        DbContextOptions<NotificationsDbContext> options =
            (DbContextOptions<NotificationsDbContext>)new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseWakeParkNpgsql(connectionString, NotificationsDbContext.Schema)
                .Options;

        return new NotificationsDbContext(options);
    }
}
