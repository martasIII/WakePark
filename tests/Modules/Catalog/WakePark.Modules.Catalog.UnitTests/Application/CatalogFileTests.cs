using FluentValidation.Results;

using Shouldly;

using WakePark.Modules.Catalog.Application.Import;

namespace WakePark.Modules.Catalog.UnitTests.Application;

/// <summary>
/// Kontrola skutečného <c>katalog.json</c> — výchozích dat katalogu, která nasazení doplní
/// do databáze. Překlep v ceně, chybějící obrázek nebo překlad tak neprojde CI.
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

    [Fact]
    public async Task Je_tu_sekce_vouchery_na_kterou_vedou_stare_odkazy_webu()
    {
        CatalogDefinition catalog = await ReadAsync();

        // Stará adresa /shop/ přesměruje na /cenik#vouchery (LegacyRedirects ve Storefrontu).
        catalog.Categories.ShouldContain(c => c.Code == "vouchery" && c.Parent == null);
    }

    [Fact]
    public async Task Kazdy_cesky_text_ma_anglicky_preklad()
    {
        CatalogDefinition catalog = await ReadAsync();

        IEnumerable<(string Where, IReadOnlyDictionary<string, string>? Text)> texts =
        [
            ("title", catalog.Title),
            .. catalog.Categories.SelectMany(c => new[] { ($"{c.Code}.name", (IReadOnlyDictionary<string, string>?)c.Name), ($"{c.Code}.description", c.Description) }),
            .. catalog.Products.SelectMany(p => new[] { ($"{p.Code}.name", (IReadOnlyDictionary<string, string>?)p.Name), ($"{p.Code}.description", p.Description), ($"{p.Code}.badge", p.Badge) }),
        ];

        string[] missing = [.. texts.Where(t => t.Text is not null && !t.Text.ContainsKey("en")).Select(t => t.Where)];

        missing.ShouldBeEmpty("angličtina je na webu zapnutá");
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
