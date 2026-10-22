using System.ComponentModel.DataAnnotations;

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

    /// <summary>Chyby objednávky pro zákazníka — z formuláře i z API.</summary>
    public IReadOnlyList<string> Errors { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Catalog = await catalogClient.GetCatalogAsync(cancellationToken).ConfigureAwait(false);

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

        if (errors.Count > 0)
        {
            Errors = errors.Distinct(StringComparer.Ordinal).ToList();
            return Page();
        }

        OrderRequest request = new(
            SiteView.Language,
            new OrderCustomer(Form.Name, Form.Email, Form.Phone, Form.CompanyName, Form.CompanyId, Form.VatId),
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

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [StringLength(12)]
    public string? CompanyId { get; set; }

    [StringLength(14)]
    public string? VatId { get; set; }

    public bool TermsAccepted { get; set; }

    /// <summary>Past na roboty: pole je skryté, člověk ho nevyplní.</summary>
    public string? Website { get; set; }
}
