using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Identity.Infrastructure.Persistence;

namespace WakePark.Api.Infrastructure;

/// <summary>
/// Naplní oprávnění, systémové role a volitelně prvního správce.
/// </summary>
/// <remarks>
/// Spouští se jen ve vývojovém prostředí. V produkci se seeder pouští jako samostatný krok
/// nasazení — aplikace, která si při startu sama mění data, se špatně škáluje do víc instancí.
///
/// Migrace se neaplikují automaticky. Když schéma chybí, seeder to řekne a nepokračuje;
/// tiše si databázi měnit nebude.
/// </remarks>
internal static class IdentitySeedExtensions
{
    public static async Task SeedIdentityAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");
        IdentityDbContext dbContext = services.GetRequiredService<IdentityDbContext>();

        if (!await dbContext.Database.CanConnectAsync().ConfigureAwait(false))
        {
            logger.LogWarning("Databáze není dostupná, seed identity se přeskakuje.");
            return;
        }

        IEnumerable<string> pending = await dbContext.Database
            .GetPendingMigrationsAsync()
            .ConfigureAwait(false);

        if (pending.Any())
        {
            logger.LogWarning(
                "Schéma identity není aktuální, seed se přeskakuje. Spusť: dotnet dotnet-ef database update "
                + "--project src/backend/Modules/Identity/WakePark.Modules.Identity.Infrastructure "
                + "--context IdentityDbContext");
            return;
        }

        IConfiguration configuration = app.Configuration;

        Guid? enterpriseId = Guid.TryParse(configuration["Identity:BootstrapAdmin:EnterpriseId"], out Guid parsed)
            ? parsed
            : null;

        await services.GetRequiredService<IdentitySeeder>()
            .SeedAsync(
                configuration["Identity:BootstrapAdmin:Email"],
                configuration["Identity:BootstrapAdmin:Password"],
                enterpriseId)
            .ConfigureAwait(false);
    }
}
