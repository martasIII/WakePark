using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Catalog.Infrastructure.Persistence;
using WakePark.Modules.Identity.Infrastructure.Persistence;
using WakePark.Modules.Operations.Infrastructure.Persistence;
using WakePark.Modules.Reservations.Infrastructure.Persistence;
using WakePark.Modules.Tenancy.Infrastructure.Persistence;

namespace WakePark.Api.Infrastructure;

/// <summary>
/// Aplikuje migrace všech modulů. Krok nasazení (<c>dotnet WakePark.Api.dll migrate</c>),
/// spouští se před startem nové verze — aplikace si schéma při startu sama nemění.
/// </summary>
/// <remarks>
/// Každý modul má vlastní schéma a historii migrací, takže na pořadí nezáleží.
/// Migrace musí být zpětně kompatibilní: chvíli proti novému schématu běží ještě stará verze.
/// </remarks>
internal static partial class MigrationExtensions
{
    public const string CommandName = "migrate";

    public static async Task MigrateAllModulesAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

        DbContext[] contexts =
        [
            services.GetRequiredService<IdentityDbContext>(),
            services.GetRequiredService<TenancyDbContext>(),
            services.GetRequiredService<OperationsDbContext>(),
            services.GetRequiredService<ReservationsDbContext>(),
            services.GetRequiredService<CatalogDbContext>(),
        ];

        foreach (DbContext context in contexts)
        {
            // Na historii se ptáme až po migraci, kdy tabulka historie určitě existuje.
            // Na úplně prázdné databázi EF i tak zaloguje jednu „Failed executing DbCommand“
            // na modul — zkouší číst historii dřív, než ji založí. Je to neškodné a jen poprvé.
            await context.Database.MigrateAsync().ConfigureAwait(false);
            int applied = (await context.Database.GetAppliedMigrationsAsync().ConfigureAwait(false)).Count();
            LogMigrated(logger, context.GetType().Name, applied);
        }
    }

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "{Context}: schéma aktuální, migrací celkem {Count}")]
    private static partial void LogMigrated(ILogger logger, string context, int count);
}
