using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Reservations.Infrastructure.Persistence;

namespace WakePark.Modules.Reservations.IntegrationTests;

/// <summary>
/// Čerstvý PostgreSQL v kontejneru pro celou testovací třídu. Migrace se aplikují stejné,
/// jaké půjdou do produkce — testovat proti in-memory provideru nemá cenu, protože se chová
/// jinak než reálná databáze a testy by lhaly.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);

        await using ReservationsDbContext dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    public ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options =
            (DbContextOptions<ReservationsDbContext>)new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseWakeParkNpgsql(ConnectionString, ReservationsDbContext.Schema)
                .Options;

        return new ReservationsDbContext(options);
    }
}
