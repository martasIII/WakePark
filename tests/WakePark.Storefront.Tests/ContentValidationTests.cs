using System.Text;

using WakePark.Storefront.Content;

namespace WakePark.Storefront.Tests;

/// <summary>Validace musí chytit chyby, které se při ruční úpravě JSONu dělají nejčastěji.</summary>
public sealed class ContentValidationTests
{
    [Fact]
    public void Text_muze_byt_retezec_i_objekt_s_preklady()
    {
        PriceList list = Parse<PriceList>("""
            { "sections": [ { "name": "Jízdné", "items": [
                { "name": { "cs": "20 minut", "en": "20 minutes" }, "price": 500 } ] } ] }
            """);

        PriceSection section = list.Sections[0];
        section.Name.Resolve("en").ShouldBe("Jízdné");
        section.Items![0].Name.Resolve("en").ShouldBe("20 minutes");
        section.Items[0].Name.Resolve("cs").ShouldBe("20 minut");
    }

    [Fact]
    public void Chybejici_preklad_se_zobrazi_cesky()
    {
        LocalizedText text = LocalizedText.Of(new Dictionary<string, string> { ["cs"] = "Zavřeno" });

        text.Resolve("de").ShouldBe("Zavřeno");
    }

    [Fact]
    public void Polozka_bez_ceny_neprojde()
    {
        PriceList list = Parse<PriceList>("""
            { "sections": [ { "name": "Jízdné", "items": [ { "name": "20 minut" } ] } ] }
            """);

        list.Validate().ShouldHaveSingleItem().ShouldContain("chybí cena");
    }

    [Fact]
    public void Web_nacte_jen_obsah_ktery_projde_kontrolou()
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("""
            { "sections": [ { "name": "Jízdné", "items": [ { "name": "20 minut" } ] } ] }
            """));

        InvalidDataException error = Should.Throw<InvalidDataException>(
            () => ContentJson.ReadValid<PriceList>(stream, "cenik.json"));

        error.Message.ShouldStartWith("cenik.json:");
        error.Message.ShouldContain("chybí cena");
    }

    [Fact]
    public void Zaporna_cena_a_duplicitni_id_neprojdou()
    {
        PriceList list = Parse<PriceList>("""
            { "sections": [
                { "id": "jizdne", "name": "Jízdné", "items": [ { "name": "20 minut", "price": -1 } ] },
                { "id": "jizdne", "name": "Půjčovna", "items": [ { "name": "Helma", "price": 0 } ] } ] }
            """);

        IReadOnlyList<string> errors = list.Validate();

        errors.Count.ShouldBe(2);
        errors.ShouldContain(e => e.Contains("záporná cena", StringComparison.Ordinal));
        errors.ShouldContain(e => e.Contains("už má jiná sekce", StringComparison.Ordinal));
    }

    [Fact]
    public void Rozbity_json_hlasi_radek_chyby()
    {
        InvalidDataException error = Should.Throw<InvalidDataException>(() => Parse<PriceList>("""
            { "sections": [
                { "name": "Jízdné", "items": [ { "name": "20 minut", "price": 500 }, ] } ] }
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
