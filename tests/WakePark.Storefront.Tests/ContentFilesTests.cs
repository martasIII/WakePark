using WakePark.Storefront.Content;

namespace WakePark.Storefront.Tests;

/// <summary>
/// Kontroluje skutečné soubory s obsahem webu. Otevírací dobu upravuje vlastník ručně; když
/// v ní bude chyba, spadne CI a rozbitý obsah se na web nedostane. Ceník je v katalogu API
/// a jeho výchozí data kontroluje <c>CatalogFileTests</c>.
/// </summary>
public sealed class ContentFilesTests
{
    [Fact]
    public void Otevíraci_doba_je_platna()
    {
        OpeningHours hours = Read<OpeningHours>(SiteContent.OpeningHoursFile);

        hours.Validate().ShouldBeEmpty();
    }

    private static T Read<T>(string fileName) where T : class
    {
        using FileStream stream = File.OpenRead(Path.Combine(DataDirectory(), fileName));
        return ContentJson.Read<T>(stream, fileName);
    }

    /// <summary>Zdrojová složka s daty, ne kopie v bin — testuje se to, co je v gitu.</summary>
    private static string DataDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WakePark.sln")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("Nenašel jsem kořen repa (WakePark.sln).");
        return Path.Combine(directory.FullName, "src", "frontend", "WakePark.Storefront", "data");
    }
}
