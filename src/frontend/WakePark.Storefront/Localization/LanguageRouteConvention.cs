using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace WakePark.Storefront.Localization;

/// <summary>
/// Každé stránce přidá druhou adresu s prefixem jazyka: <c>/cenik</c> → <c>/en/cenik</c>.
/// Výchozí jazyk prefix nemá. Jazyk je v adrese, aby vyhledávače viděly každou jazykovou
/// verzi zvlášť (viz ADR 0006, část Jazyky).
/// </summary>
public sealed class LanguageRouteConvention(IReadOnlyList<string> prefixedLanguages) : IPageRouteModelConvention
{
    public const string RouteKey = "lang";

    public void Apply(PageRouteModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (prefixedLanguages.Count == 0)
        {
            return;
        }

        string prefix = $"{{{RouteKey}:regex(^({string.Join('|', prefixedLanguages)})$)}}";

        foreach (SelectorModel selector in model.Selectors.ToList())
        {
            AttributeRouteModel? original = selector.AttributeRouteModel;
            model.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel
                {
                    Template = AttributeRouteModel.CombineTemplates(prefix, original?.Template),
                    // Přednost před adresou bez prefixu — jinak by odkaz s lang=en vyšel
                    // jako /cenik?lang=en místo /en/cenik.
                    Order = -1,
                    // /Index je jen záložní adresa úvodní stránky; odkazy mají vést na /en.
                    SuppressLinkGeneration = original?.SuppressLinkGeneration ?? false,
                    SuppressPathMatching = original?.SuppressPathMatching ?? false,
                },
            });
        }
    }
}
