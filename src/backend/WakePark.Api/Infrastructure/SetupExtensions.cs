using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Identity.Infrastructure.Persistence;
using WakePark.Modules.Operations.Application.Setup;
using WakePark.Modules.Tenancy.Infrastructure.Persistence;

namespace WakePark.Api.Infrastructure;

/// <summary>
/// Kroky nasazení pro základní data provozu (ADR 0009, upřesnění 24. 10.):
/// <c>import-setup</c> srovná provozovatele, areál, nastavení a vleky s <c>data/provoz.json</c>;
/// <c>create-admin &lt;e-mail&gt;</c> založí admina (heslo se zadá v terminálu, nikde jinde není).
/// </summary>
internal static partial class SetupExtensions
{
    public const string ImportCommandName = "import-setup";
    public const string CreateAdminCommandName = "create-admin";
    public const int MinPasswordLength = 12;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static async Task<bool> ImportSetupAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Setup");

        if (!TryGetEnterpriseId(app, logger, out Guid enterpriseId))
        {
            return false;
        }

        string path = Path.Combine(app.Environment.ContentRootPath, app.Configuration["Setup:FilePath"] ?? Path.Combine("data", "provoz.json"));

        SetupFile setup;
        await using (FileStream file = File.OpenRead(path))
        {
            setup = await JsonSerializer.DeserializeAsync<SetupFile>(file, JsonOptions).ConfigureAwait(false)
                ?? throw new JsonException("Soubor provozu je prázdný.");
        }

        EnterpriseFile e = setup.Enterprise;
        await services.GetRequiredService<TenancySetupApplier>()
            .ApplyAsync(
                new EnterpriseSetup(enterpriseId, e.Name, e.RegistrationNumber, e.VatNumber, e.Address, e.BankAccountNumber, e.Iban),
                setup.Park)
            .ConfigureAwait(false);

        OperationsSetupResult result = await services
            .GetRequiredService<ICommandHandler<ApplyOperationsSetupCommand, OperationsSetupResult>>()
            .HandleAsync(new ApplyOperationsSetupCommand(setup.Park.Id, new OperationsSetup(setup.Settings, setup.Cables)))
            .ConfigureAwait(false);

        LogImported(logger, path, setup.Park.Name, result);
        return true;
    }

    public static async Task<bool> CreateAdminAsync(this WebApplication app, string[] args)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(args);

        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Setup");

        int index = Array.IndexOf(args, CreateAdminCommandName);
        string? email = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            await Console.Error.WriteLineAsync($"Použití: {CreateAdminCommandName} <e-mail>").ConfigureAwait(false);
            return false;
        }

        if (!TryGetEnterpriseId(app, logger, out Guid enterpriseId))
        {
            return false;
        }

        if (!await services.GetRequiredService<TenancyDbContext>().Enterprises.AnyAsync(x => x.Id == enterpriseId).ConfigureAwait(false))
        {
            await Console.Error.WriteLineAsync($"Provozovatel {enterpriseId} neexistuje — nejdřív {ImportCommandName}.").ConfigureAwait(false);
            return false;
        }

        string password = ReadPassword("Heslo (aspoň " + MinPasswordLength + " znaků): ");
        if (password.Length < MinPasswordLength)
        {
            await Console.Error.WriteLineAsync($"Heslo musí mít aspoň {MinPasswordLength} znaků.").ConfigureAwait(false);
            return false;
        }

        if (!string.Equals(password, ReadPassword("Heslo znovu: "), StringComparison.Ordinal))
        {
            await Console.Error.WriteLineAsync("Hesla se neshodují.").ConfigureAwait(false);
            return false;
        }

        Guid userId = await services.GetRequiredService<IdentitySeeder>()
            .EnsureAdminAsync(email, password, enterpriseId)
            .ConfigureAwait(false);

        bool owner = await services.GetRequiredService<TenancySetupApplier>()
            .ClaimOwnershipAsync(enterpriseId, userId)
            .ConfigureAwait(false);

        LogAdminReady(logger, email.Trim().ToLowerInvariant(), owner);
        return true;
    }

    private static bool TryGetEnterpriseId(WebApplication app, ILogger logger, out Guid enterpriseId)
    {
        if (Guid.TryParse(app.Configuration["Catalog:EnterpriseId"], out enterpriseId))
        {
            return true;
        }

        LogNoEnterprise(logger);
        return false;
    }

    /// <summary>Heslo z terminálu bez vypisování. Bez terminálu (přesměrovaný vstup) prostý řádek.</summary>
    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);

        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        StringBuilder password = new();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }

    [LoggerMessage(EventId = 2201, Level = LogLevel.Information, Message = "Provoz naimportován z {Path}: areál {Park}, {Result}")]
    private static partial void LogImported(ILogger logger, string path, string park, OperationsSetupResult result);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Warning, Message = "Admin {Email} je připravený (vlastník provozovatele: {Owner}).")]
    private static partial void LogAdminReady(ILogger logger, string email, bool owner);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Error, Message = "Není nastavené Catalog:EnterpriseId — provozovatel není jasný.")]
    private static partial void LogNoEnterprise(ILogger logger);

    private sealed record SetupFile(EnterpriseFile Enterprise, ParkSetup Park, ParkSettingsSetup Settings, IReadOnlyList<CableSetup> Cables);

    private sealed record EnterpriseFile(
        string Name,
        string RegistrationNumber,
        string? VatNumber,
        AddressSetup Address,
        string? BankAccountNumber,
        string? Iban);
}
