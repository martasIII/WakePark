using FluentValidation;

using Microsoft.EntityFrameworkCore;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Catalog.Application.Import;
using WakePark.Modules.Catalog.Infrastructure.Persistence;

namespace WakePark.Api.Infrastructure;

/// <summary>
/// Nahraje nabídku e-shopu ze souboru <c>katalog.json</c> do databáze (ADR 0006).
/// </summary>
/// <remarks>
/// Ve vývoji běží při startu, stejně jako seed identity. Na serveru je to samostatný krok
/// nasazení (<c>dotnet WakePark.Api.dll import-catalog</c>) — aplikace si při startu sama data
/// nemění. Migrace se ani tady neaplikují automaticky.
/// </remarks>
internal static partial class CatalogImportExtensions
{
    public const string CommandName = "import-catalog";

    /// <returns><c>true</c>, pokud import proběhl, nebo nebylo co importovat.</returns>
    public static async Task<bool> ImportCatalogAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogImport");

        if (!Guid.TryParse(app.Configuration["Catalog:EnterpriseId"], out Guid enterpriseId))
        {
            logger.LogWarning("Není nastavené Catalog:EnterpriseId, import katalogu se přeskakuje.");
            return true;
        }

        string path = Path.Combine(
            app.Environment.ContentRootPath,
            app.Configuration["Catalog:FilePath"] ?? Path.Combine("data", "katalog.json"));

        CatalogDbContext dbContext = services.GetRequiredService<CatalogDbContext>();

        if (!await dbContext.Database.CanConnectAsync().ConfigureAwait(false))
        {
            logger.LogWarning("Databáze není dostupná, import katalogu se přeskakuje.");
            return false;
        }

        if ((await dbContext.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).Any())
        {
            logger.LogWarning(
                "Schéma katalogu není aktuální, import se přeskakuje. Spusť: dotnet dotnet-ef database update "
                + "--project src/backend/Modules/Catalog/WakePark.Modules.Catalog.Infrastructure "
                + "--context CatalogDbContext");
            return false;
        }

        try
        {
            CatalogDefinition definition;
            await using (FileStream file = File.OpenRead(path))
            {
                definition = await CatalogDefinition.ReadAsync(file).ConfigureAwait(false);
            }

            CatalogImportResult result = await services
                .GetRequiredService<ICommandHandler<ImportCatalogCommand, CatalogImportResult>>()
                .HandleAsync(new ImportCatalogCommand(enterpriseId, definition))
                .ConfigureAwait(false);

            LogImported(logger, path, result);
            return true;
        }
        catch (ValidationException exception)
        {
            LogInvalid(logger, path, exception.Message);
            return false;
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Katalog naimportován z {Path}: {Result}")]
    private static partial void LogImported(ILogger logger, string path, CatalogImportResult result);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Katalog {Path} je neplatný, nic se neuložilo: {Errors}")]
    private static partial void LogInvalid(ILogger logger, string path, string errors);
}
