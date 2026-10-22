using System.Text.Json;
using System.Xml.Linq;

namespace WakePark.Storefront.Tests;

/// <summary>
/// Angličtina je zapnutá — žádný text nesmí zůstat jen česky. Chybějící překlad by se sice
/// zobrazil česky (stránka se nerozbije), ale anglický návštěvník by dostal půl webu česky.
/// </summary>
public sealed class TranslationCompletenessTests
{
    private static readonly string StorefrontRoot = Path.Combine(FindRepoRoot(), "src", "frontend", "WakePark.Storefront");

    [Theory]
    [InlineData("cenik.json")]
    [InlineData("oteviraci-doba.json")]
    public void Kazdy_cesky_text_v_obsahu_ma_anglicky_preklad(string file)
    {
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(StorefrontRoot, "data", file)));

        List<string> missing = [];
        Walk(json.RootElement, "$", missing);

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void Kazdy_text_rozhrani_ma_anglicky_preklad()
    {
        string resources = Path.Combine(StorefrontRoot, "Resources");
        HashSet<string> czech = Keys(Path.Combine(resources, "Localization.SharedResource.resx"));
        HashSet<string> english = Keys(Path.Combine(resources, "Localization.SharedResource.en.resx"));

        czech.Except(english).ShouldBeEmpty("chybí v .en.resx");
        english.Except(czech).ShouldBeEmpty("je navíc v .en.resx (překlep v klíči?)");
    }

    private static void Walk(JsonElement element, string path, List<string> missing)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("cs", out _) && !element.TryGetProperty("en", out _))
            {
                missing.Add(path);
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                Walk(property.Value, $"{path}.{property.Name}", missing);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                Walk(item, $"{path}[{index++}]", missing);
            }
        }
    }

    private static HashSet<string> Keys(string resx) =>
        [.. XDocument.Load(resx).Root!.Elements("data").Select(d => (string)d.Attribute("name")!)];

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WakePark.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Nenašel jsem kořen repa.");
    }
}
