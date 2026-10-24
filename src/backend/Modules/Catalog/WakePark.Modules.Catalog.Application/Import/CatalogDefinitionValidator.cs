using FluentValidation;

using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.Application.Import;

/// <summary>
/// Kontrola souboru katalogu jako celku. Doména hlídá každý produkt zvlášť; tady se hlídá to,
/// co jednotlivý produkt nevidí — duplicitní kódy, neexistující kategorie, hloubka stromu,
/// česká sazba DPH, kladná cena v e-shopu.
/// Běží v testech nad skutečným souborem, takže chyba neprojde CI.
/// </summary>
public sealed class CatalogDefinitionValidator : AbstractValidator<CatalogDefinition>
{
    /// <summary>Sazby DPH v Česku od roku 2024. Ověří účetní u konkrétních produktů.</summary>
    public static readonly IReadOnlySet<decimal> CzechVatRates = new HashSet<decimal> { 0m, 12m, 21m };

    /// <summary>Hodnoty <c>showIn</c> v souboru.</summary>
    public static readonly IReadOnlySet<string> Channels = new HashSet<string>(StringComparer.Ordinal) { "pricelist", "eshop", "pos" };

    public CatalogDefinitionValidator()
    {
        RuleFor(c => c.Currency).Equal("CZK").WithMessage("E-shop zatím prodává jen v CZK.");

        RuleFor(c => c.Categories).NotEmpty().WithMessage("Katalog musí mít aspoň jednu kategorii.");
        RuleFor(c => c.Products).NotEmpty().WithMessage("Katalog musí mít aspoň jeden produkt.");

        RuleForEach(c => c.Categories).ChildRules(category =>
        {
            category.RuleFor(c => c.Code).Must(CatalogCode.IsValid)
                .WithMessage(c => $"Kategorie '{c.Code}': kód smí mít jen malá písmena, číslice a pomlčky.");
            category.RuleFor(c => c.Name).Must(HasCzech)
                .WithMessage(c => $"Kategorie '{c.Code}': chybí český název.");
            category.RuleFor(c => c.Parent).NotEqual(c => c.Code)
                .WithMessage(c => $"Kategorie '{c.Code}': nemůže být podkategorií sama sebe.");
        });

        RuleForEach(c => c.Products).ChildRules(product =>
        {
            product.RuleFor(p => p.Code).Must(CatalogCode.IsValid)
                .WithMessage(p => $"Produkt '{p.Code}': kód smí mít jen malá písmena, číslice a pomlčky.");
            product.RuleFor(p => p.Name).Must(HasCzech)
                .WithMessage(p => $"Produkt '{p.Code}': chybí český název.");
            product.RuleFor(p => p.Description).Must(HasCzech)
                .When(p => p.Description is not null)
                .WithMessage(p => $"Produkt '{p.Code}': popis je bez češtiny.");
            product.RuleFor(p => p.Price).GreaterThanOrEqualTo(0m)
                .WithMessage(p => $"Produkt '{p.Code}': cena nesmí být záporná.");
            product.RuleFor(p => p.Price).GreaterThan(0m)
                .When(p => p.ShowIn?.Contains("eshop") == true)
                .WithMessage(p => $"Produkt '{p.Code}': v e-shopu musí mít kladnou cenu.");
            product.RuleFor(p => p.ShowIn).NotNull()
                .WithMessage(p => $"Produkt '{p.Code}': chybí, kde se ukazuje (\"showIn\": pricelist, eshop, pos).");
            product.RuleForEach(p => p.ShowIn).Must(Channels.Contains)
                .WithMessage((p, channel) => $"Produkt '{p.Code}': neznámé místo zobrazení '{channel}' (pricelist, eshop, pos).");
            product.RuleFor(p => p.Price).Must(price => price == decimal.Round(price, 2))
                .WithMessage(p => $"Produkt '{p.Code}': cena smí mít nejvýš dvě desetinná místa.");
            product.RuleFor(p => p.VatRate).Must(CzechVatRates.Contains)
                .WithMessage(p => $"Produkt '{p.Code}': sazba DPH {p.VatRate} % v Česku neexistuje (0, 12, 21).");
            product.RuleFor(p => p.Image)
                .Must(image => image is null || (image.StartsWith('/') && !image.Contains("..", StringComparison.Ordinal)))
                .WithMessage(p => $"Produkt '{p.Code}': obrázek musí být cesta na webu začínající lomítkem.");
        });

        RuleFor(c => c.Categories).Must(categories => Duplicates(categories.Select(c => c.Code)).Count == 0)
            .WithMessage(c => $"Duplicitní kód kategorie: {string.Join(", ", Duplicates(c.Categories.Select(x => x.Code)))}.")
            .When(c => c.Categories is not null);

        RuleFor(c => c.Products).Must(products => Duplicates(products.Select(p => p.Code)).Count == 0)
            .WithMessage(c => $"Duplicitní kód produktu: {string.Join(", ", Duplicates(c.Products.Select(x => x.Code)))}.")
            .When(c => c.Products is not null);

        // Kategorie a podkategorie, víc ne (CategoryTree.MaxDepth): rodič musí být nejvyšší úrovně.
        RuleForEach(c => c.Categories)
            .Must((catalog, category) => category.Parent is null
                || catalog.Categories.Any(parent => parent.Code == category.Parent && parent.Parent is null))
            .WithMessage((_, category) => $"Kategorie '{category.Code}': nadřazená '{category.Parent}' v katalogu není, nebo je sama podkategorií.")
            .When(c => c.Categories is not null);

        RuleForEach(c => c.Products)
            .Must((catalog, product) => catalog.Categories.Any(category => category.Code == product.Category))
            .WithMessage((_, product) => $"Produkt '{product.Code}': kategorie '{product.Category}' v katalogu není.")
            .When(c => c.Categories is not null);
    }

    private static bool HasCzech(IReadOnlyDictionary<string, string>? text) =>
        text is not null
        && text.TryGetValue(LocalizedText.RequiredLanguage, out string? czech)
        && !string.IsNullOrWhiteSpace(czech);

    private static List<string> Duplicates(IEnumerable<string> codes) =>
        [.. codes.GroupBy(code => code, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key)];
}
