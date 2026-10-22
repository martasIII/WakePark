using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

using WakePark.Storefront.Eshop;
using WakePark.Storefront.Infrastructure;
using WakePark.Storefront.Localization;

namespace WakePark.Storefront.Pages;

public sealed partial class EshopModel(
    CatalogClient catalogClient,
    OrderClient orderClient,
    CompanyClient companyClient,
    IStringLocalizer<SharedResource> text,
    ILogger<EshopModel> logger) : PageModel
{
    /// <summary>Null, když API neodpovídá nebo e-shop není nastavený — stránka pak ukáže telefon.</summary>
    public EshopCatalog? Catalog { get; private set; }

    /// <summary>Počet kusů podle Id produktu.</summary>
    [BindProperty]
    public Dictionary<Guid, int> Quantities { get; set; } = [];

    [BindProperty]
    public OrderForm Form { get; set; } = new();

    /// <summary>Firma z ARES k zadanému IČO — jen k zobrazení, na doklad ji bere API samo.</summary>
    public CompanyInfo? Company { get; private set; }

    /// <summary>Chyby objednávky pro zákazníka — z formuláře i z API.</summary>
    public IReadOnlyList<string> Errors { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Catalog = await catalogClient.GetCatalogAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Náhled firmy pro formulář (volá eshop.js při psaní IČO). Bez skriptu se firma ověří
    /// a ukáže až po odeslání.
    /// </summary>
    public async Task<IActionResult> OnGetCompanyAsync(string? ico, CancellationToken cancellationToken)
    {
        string companyId = (ico ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!CompanyIdPattern().IsMatch(companyId))
        {
            return NotFound();
        }

        CompanyLookup lookup = await companyClient.FindAsync(companyId, cancellationToken).ConfigureAwait(false);
        return lookup.Status switch
        {
            CompanyLookupStatus.Found => new JsonResult(lookup.Company),
            CompanyLookupStatus.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable),
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Skryté pole vyplní jen robot. Tváříme se, že se nic nestalo, ať nemá co ladit.
        if (!string.IsNullOrEmpty(Form.Website))
        {
            LogHoneypot();
            return RedirectToPage("/Eshop", new { lang = HttpContext.GetRouteValue("lang") });
        }

        Catalog = await catalogClient.GetCatalogAsync(cancellationToken).ConfigureAwait(false);

        List<OrderRequestLine> lines =
        [
            .. Quantities.Where(q => q.Value > 0).Select(q => new OrderRequestLine(q.Key, q.Value)),
        ];

        List<string> errors = [.. ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)];
        if (lines.Count == 0)
        {
            errors.Insert(0, text["ValidationNoProduct"]);
        }

        // Na firmu je IČO povinné. Firmu ověříme už tady, ať chyba přijde v jazyce stránky
        // a po návratu do formuláře jsou vidět název, sídlo a DIČ.
        string? companyId = null;
        if (Form.IsCompany)
        {
            companyId = Form.CompanyId?.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (string.IsNullOrEmpty(companyId))
            {
                errors.Add(text["ValidationCompanyIdRequired"]);
            }
            else
            {
                CompanyLookup lookup = CompanyIdPattern().IsMatch(companyId)
                    ? await companyClient.FindAsync(companyId, cancellationToken).ConfigureAwait(false)
                    : new CompanyLookup(CompanyLookupStatus.NotFound);

                Company = lookup.Company;
                if (lookup.Status == CompanyLookupStatus.NotFound)
                {
                    errors.Add(text["CompanyNotFound"]);
                }
                else if (lookup.Status == CompanyLookupStatus.Unavailable)
                {
                    errors.Add(text["CompanyUnavailable"]);
                }
            }
        }

        if (errors.Count > 0)
        {
            Errors = errors.Distinct(StringComparer.Ordinal).ToList();
            return Page();
        }

        OrderRequest request = new(
            SiteView.Language,
            new OrderCustomer(Form.Name, Form.Email, Form.Phone, companyId),
            lines,
            Form.TermsAccepted);

        PlaceOrderResult result = await orderClient.PlaceAsync(request, cancellationToken).ConfigureAwait(false);

        if (result.Order is null)
        {
            // Technickou chybu říká web v jazyce stránky; hlášky API (např. neplatné IČO) zatím česky.
            Errors = [.. result.Errors.Select(e => e == OrderClient.UnavailableMessage ? text["OrderUnavailable"].Value : e)];
            return Page();
        }

        return RedirectToPage("/Objednavka", new { token = result.Order.AccessToken, lang = HttpContext.GetRouteValue("lang") });
    }

    public IEnumerable<EshopProduct> ProductsIn(EshopCategory category) =>
        Catalog?.Products.Where(p => p.CategoryCode == category.Code) ?? [];

    public int QuantityOf(EshopProduct product) =>
        Quantities.TryGetValue(product.Id, out int quantity) ? quantity : 0;

    [GeneratedRegex("^[0-9]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex CompanyIdPattern();

    [LoggerMessage(Level = LogLevel.Information, Message = "Objednávka s vyplněným skrytým polem — nejspíš robot, zahozena.")]
    private partial void LogHoneypot();
}

/// <summary>Kontaktní údaje z formuláře. Pravidla tady jsou jen první síto, rozhoduje API.</summary>
public sealed class OrderForm
{
    [Required(ErrorMessage = "ValidationName")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "ValidationEmailRequired")]
    [EmailAddress(ErrorMessage = "ValidationEmail")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Phone { get; set; }

    /// <summary>Nakupuji na firmu. Jen pak se IČO posílá a je povinné.</summary>
    public bool IsCompany { get; set; }

    /// <summary>IČO. Název, sídlo a DIČ se nevyplňují — doplní je API z ARES.</summary>
    [StringLength(12)]
    public string? CompanyId { get; set; }

    public bool TermsAccepted { get; set; }

    /// <summary>Past na roboty: pole je skryté, člověk ho nevyplní.</summary>
    public string? Website { get; set; }
}
