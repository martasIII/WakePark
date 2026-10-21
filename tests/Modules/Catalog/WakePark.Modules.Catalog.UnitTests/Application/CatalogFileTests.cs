using FluentValidation.Results;

using Shouldly;

using WakePark.Modules.Catalog.Application.Import;

namespace WakePark.Modules.Catalog.UnitTests.Application;

/// <summary>
/// Kontrola skutečného <c>katalog.json</c>. Dokud se nabídka mění úpravou souboru, je tohle
/// jediná pojistka, že překlep v ceně nebo chybějící obrázek neprojde CI a nedostane se na web.
/// </summary>
public sealed class CatalogFileTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string CatalogPath =
        Path.Combine(RepoRoot, "src", "backend", "WakePark.Api", "data", "katalog.json");

    private static readonly string StorefrontWebRoot =
        Path.Combine(RepoRoot, "src", "frontend", "WakePark.Storefront", "wwwroot");

    [Fact]
    public async Task Katalog_jde_precist_a_projde_validaci()
    {
        CatalogDefinition catalog = await ReadAsync();

        ValidationResult result = await new CatalogDefinitionValidator().ValidateAsync(catalog, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public async Task Kazdy_obrazek_existuje_na_webu()
    {
        CatalogDefinition catalog = await ReadAsync();

        string[] missing =
        [
            .. catalog.Products
                .Where(p => p.Image is not null)
                .Where(p => !File.Exists(Path.Combine(StorefrontWebRoot, p.Image!.TrimStart('/'))))
                .Select(p => $"{p.Code}: {p.Image}"),
        ];

        missing.ShouldBeEmpty("obrázek z katalogu chybí ve wwwroot Storefrontu");
    }

    private static async Task<CatalogDefinition> ReadAsync()
    {
        await using FileStream file = File.OpenRead(CatalogPath);
        return await CatalogDefinition.ReadAsync(file, TestContext.Current.CancellationToken);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WakePark.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Nenašel jsem kořen repa (WakePark.sln).");
    }
}
