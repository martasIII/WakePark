using System.Globalization;
using System.Net;
using System.Text;

using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Vouchers.Contracts;

namespace WakePark.Modules.Notifications.Application.Orders;

/// <summary>E-mail po zaplacení: poděkování, seznam poukazů s kódy a platností, PDF v přílohách.</summary>
internal static class VouchersIssuedEmail
{
    public const string Kind = "vouchers-issued";

    public static (string Subject, string Html, string Text) Render(
        OrderDto order,
        IReadOnlyList<VoucherDto> vouchers,
        InvoiceDto? invoice,
        Uri orderUrl,
        NotificationsOptions options)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(vouchers);
        ArgumentNullException.ThrowIfNull(orderUrl);
        ArgumentNullException.ThrowIfNull(options);

        bool en = order.Language == "en";
        CultureInfo culture = CultureInfo.GetCultureInfo(en ? "en" : "cs");
        string Date(DateOnly date) => date.ToString(en ? "d MMM yyyy" : "d. M. yyyy", culture);

        string subject = en
            ? $"Your vouchers — order {order.Number}"
            : $"Vaše vouchery — objednávka {order.Number}";

        string intro = en
            ? "thank you for your payment. Your vouchers are attached as PDF — print them or show them on your phone at the park."
            : "děkujeme za platbu. Vouchery máte v příloze jako PDF — vytiskněte si je, nebo je v parku ukažte v mobilu.";

        string invoiceNote = invoice is null
            ? string.Empty
            : en ? $"The tax document {invoice.Number} is attached as well." : $"V příloze je i daňový doklad {invoice.Number}.";

        string booking = en
            ? $"Book your ride in advance by phone at {options.Phone}."
            : $"Jízdu si předem zarezervujte na telefonu {options.Phone}.";

        StringBuilder text = new();
        text.AppendLine(en ? $"Hello {order.CustomerName}," : $"Dobrý den, {order.CustomerName},");
        text.AppendLine();
        text.AppendLine(intro);
        text.AppendLine();
        foreach (VoucherDto voucher in vouchers)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{voucher.Code}  {voucher.DisplayName}");
        }

        text.AppendLine();
        text.AppendLine(en
            ? $"Valid {Date(vouchers[0].ValidFrom)} – {Date(vouchers[0].ValidTo)}."
            : $"Platí od {Date(vouchers[0].ValidFrom)} do {Date(vouchers[0].ValidTo)}.");
        text.AppendLine(booking);
        if (invoiceNote.Length > 0)
        {
            text.AppendLine(invoiceNote);
        }

        text.AppendLine();
        text.AppendLine(orderUrl.ToString());
        text.AppendLine("Wake Park Džbán");

        static string H(string value) => WebUtility.HtmlEncode(value);

        StringBuilder html = new();
        html.Append("<!DOCTYPE html><html><body style=\"margin:0;padding:24px;background:#f5f5f7;font-family:Arial,sans-serif;color:#1d1d1f\">");
        html.Append("<div style=\"max-width:560px;margin:0 auto;background:#fff;border-radius:8px;padding:24px\">");
        html.Append("<h1 style=\"margin:0 0 12px;font-size:22px;color:#1a8f52\">✓ ").Append(H(subject)).Append("</h1>");
        html.Append("<p>").Append(H(en ? $"Hello {order.CustomerName}," : $"Dobrý den, {order.CustomerName},")).Append("</p>");
        html.Append("<p>").Append(H(intro)).Append("</p>");
        html.Append("<table style=\"width:100%;border-collapse:collapse;margin:16px 0\">");
        foreach (VoucherDto voucher in vouchers)
        {
            html.Append("<tr><td style=\"padding:8px 0;border-top:1px solid #eee;font-family:Consolas,monospace;font-size:16px;font-weight:bold\">")
                .Append(H(voucher.Code)).Append("</td><td style=\"padding:8px 0;border-top:1px solid #eee;text-align:right\">")
                .Append(H(voucher.DisplayName)).Append("</td></tr>");
        }

        html.Append("</table>");
        html.Append("<p><b>").Append(H(en
            ? $"Valid {Date(vouchers[0].ValidFrom)} – {Date(vouchers[0].ValidTo)}."
            : $"Platí od {Date(vouchers[0].ValidFrom)} do {Date(vouchers[0].ValidTo)}.")).Append("</b> ").Append(H(booking)).Append("</p>");
        if (invoiceNote.Length > 0)
        {
            html.Append("<p>").Append(H(invoiceNote)).Append("</p>");
        }

        html.Append("<p style=\"margin:20px 0\"><a href=\"").Append(H(orderUrl.ToString()))
            .Append("\" style=\"display:inline-block;padding:12px 18px;background:#e50051;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold\">")
            .Append(en ? "Order details" : "Detail objednávky").Append("</a></p>");
        html.Append("<p style=\"color:#6b6b72;font-size:13px\">").Append(H(en
            ? $"Questions? Reply to this e-mail or call {options.Phone}."
            : $"Dotazy? Odpovězte na tento e-mail nebo volejte {options.Phone}.")).Append("<br>Wake Park Džbán</p>");
        html.Append("</div></body></html>");

        return (subject, html.ToString(), text.ToString());
    }
}
