using System.Text;

using WakePark.Storefront.Content;

namespace WakePark.Storefront.Tests;

/// <summary>Validace musí chytit chyby, které se při ruční úpravě JSONu dělají nejčastěji.</summary>
public sealed class ContentValidationTests
{
    [Fact]
    public void Text_muze_byt_retezec_i_objekt_s_preklady()
    {
        OpeningHours hours = Parse<OpeningHours>("""
            { "week": { "mon": "10–20", "tue": "10–20", "wed": "10–20", "thu": "10–20", "fri": "10–20", "sat": "10–20", "sun": "10–20" },
              "note": { "cs": "Za bouřky zavřeno", "en": "Closed during storms" } }
            """);

        hours.Week["mon"].Resolve("en").ShouldBe("10–20");
        hours.Note!.Resolve("en").ShouldBe("Closed during storms");
        hours.Note.Resolve("cs").ShouldBe("Za bouřky zavřeno");
    }

    [Fact]
    public void Chybejici_preklad_se_zobrazi_cesky()
    {
        LocalizedText text = LocalizedText.Of(new Dictionary<string, string> { ["cs"] = "Zavřeno" });

        text.Resolve("de").ShouldBe("Zavřeno");
    }

    [Fact]
    public void Web_nacte_jen_obsah_ktery_projde_kontrolou()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("""
            { "week": { "mon": "10–20" } }
            """));

        InvalidDataException error = Should.Throw<InvalidDataException>(
            () => ContentJson.ReadValid<OpeningHours>(stream, "oteviraci-doba.json"));

        error.Message.ShouldStartWith("oteviraci-doba.json:");
    }

    [Fact]
    public void Rozbity_json_hlasi_radek_chyby()
    {
        InvalidDataException error = Should.Throw<InvalidDataException>(() => Parse<OpeningHours>("""
            {
              "week": { "mon": "10–20", "tue": "10–20", "wed": "10–20", "thu": "10–20", "fri": "10–20", "sat": "10–20", "sun": "10–20" }, }
            """));

        error.Message.ShouldContain("řádku 2");
    }

    [Fact]
    public void Otevíraci_doba_musi_mit_vsech_sedm_dni()
    {
        OpeningHours hours = Parse<OpeningHours>("""
            { "week": { "mon": "10–20", "tue": "10–20", "wed": "10–20", "thu": "10–20",
                        "fri": "10–20", "sat": "10–20", "sunday": "10–20" } }
            """);

        IReadOnlyList<string> errors = hours.Validate();

        errors.ShouldContain(e => e.Contains("„sun“", StringComparison.Ordinal));
        errors.ShouldContain(e => e.Contains("„sunday“", StringComparison.Ordinal));
    }

    private static T Parse<T>(string json) where T : class
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        return ContentJson.Read<T>(stream, "test.json");
    }
}
