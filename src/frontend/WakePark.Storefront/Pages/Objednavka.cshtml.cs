using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

using WakePark.Storefront.Eshop;

namespace WakePark.Storefront.Pages;

/// <summary>
/// Stránka objednávky — adresa obsahuje náhodný klíč, ne číslo objednávky. Kdo nezná klíč,
/// dostane 404, ať objednávka existuje, nebo ne.
/// </summary>
public sealed class ObjednavkaModel(OrderClient orderClient, IOptions<StorefrontOptions> options) : PageModel
{
    public OrderView Order { get; private set; } = null!;

    /// <summary>QR kód platby jako SVG; null, když objednávka na platbu nečeká.</summary>
    public string? QrSvg { get; private set; }

    public async Task<IActionResult> OnGetAsync(string token, CancellationToken cancellationToken)
    {
        OrderView? order = await orderClient.GetAsync(token, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return NotFound();
        }

        Order = order;
        QrSvg = order.Payment is { } payment ? PaymentQrCode.Svg(payment.Spayd) : null;
        return Page();
    }

    /// <summary>Okamžik v čase areálu — zákazník čte „do 25. 10. 18:00“, ne UTC.</summary>
    public DateTime Local(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId)).DateTime;
}
