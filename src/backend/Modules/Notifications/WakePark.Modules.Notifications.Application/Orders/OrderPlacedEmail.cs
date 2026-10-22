using System.Globalization;
using System.Net;
using System.Text;

using WakePark.Modules.Billing.Contracts;

namespace WakePark.Modules.Notifications.Application.Orders;

/// <summary>
/// Potvrzení objednávky s platebními údaji. HTML i prostý text (některé schránky obrázky ani
/// HTML neukazují). Texty zákazníka se v HTML vždy kódují — jméno z formuláře je vstup zvenku.
/// </summary>
internal static class OrderPlacedEmail
{
    public const string Kind = "order-placed";
    public const string ReminderKind = "payment-reminder";
    public const string QrContentId = "qr-platba";

    public static (string Subject, string Html, string Text) Render(
        OrderDto order,
        Uri orderUrl,
        NotificationsOptions options,
        TimeZoneInfo timeZone,
        bool reminder = false)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(orderUrl);
        ArgumentNullException.ThrowIfNull(options);

        bool en = order.Language == "en";
        CultureInfo culture = CultureInfo.GetCultureInfo(en ? "en" : "cs");
        string Price(decimal amount) => en
            ? amount.ToString("#,0.##", culture) + " CZK"
            : amount.ToString("#,0.##", culture) + " Kč";

        string subject = (reminder, en) switch
        {
            (true, true) => $"Reminder: order {order.Number} is awaiting payment",
            (true, false) => $"Připomínka: objednávka {order.Number} čeká na platbu",
            (false, true) => $"Order {order.Number} — Wake Park Džbán",
            (false, false) => $"Objednávka {order.Number} — Wake Park Džbán",
        };

        string intro = (reminder, en) switch
        {
            (true, true) => $"your order no. {order.Number} is still awaiting payment. If you have already paid, please ignore this e-mail — the payment may still be on its way.",
            (true, false) => $"objednávka č. {order.Number} zatím čeká na platbu. Pokud jste už zaplatili, tento e-mail ignorujte — platba může být ještě na cestě.",
            (false, true) => $"thank you for your order no. {order.Number}. As soon as your payment arrives, we will e-mail you the voucher.",
            (false, false) => $"děkujeme za objednávku č. {order.Number}. Jakmile dorazí platba, pošleme vám voucher e-mailem.",
        };

        PaymentInstructionsDto? payment = order.Payment;
        string due = TimeZoneInfo.ConvertTime(order.PaymentDueAtUtc, timeZone)
            .ToString(en ? "d MMM yyyy, H:mm" : "d. M. yyyy H:mm", culture);

        StringBuilder text = new();
        text.AppendLine(en ? $"Hello {order.CustomerName}," : $"Dobrý den, {order.CustomerName},");
        text.AppendLine();
        text.AppendLine(intro);
        text.AppendLine();
        foreach (OrderLineDto line in order.Lines)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{line.Quantity} × {line.Name}  {Price(line.LineGross)}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"{(en ? "Total" : "Celkem")}: {Price(order.TotalGross)}");
        text.AppendLine();

        if (payment is not null)
        {
            text.AppendLine(en ? "Payment details:" : "Platební údaje:");
            text.AppendLine(CultureInfo.InvariantCulture, $"  {(en ? "Account" : "Číslo účtu")}: {payment.AccountNumber}");
            text.AppendLine(CultureInfo.InvariantCulture, $"  IBAN: {payment.Iban}");
            text.AppendLine(CultureInfo.InvariantCulture, $"  {(en ? "Variable symbol" : "Variabilní symbol")}: {payment.VariableSymbol}");
            text.AppendLine(CultureInfo.InvariantCulture, $"  {(en ? "Amount" : "Částka")}: {Price(payment.Amount)}");
            text.AppendLine(CultureInfo.InvariantCulture, $"  {(en ? "Pay by" : "Zaplaťte do")}: {due}");
            text.AppendLine();
        }

        text.AppendLine(en ? "Order status and payment QR code:" : "Stav objednávky a QR kód pro platbu:");
        text.AppendLine(orderUrl.ToString());
        text.AppendLine();
        text.AppendLine(en ? $"Questions? Reply to this e-mail or call {options.Phone}." : $"Dotazy? Odpovězte na tento e-mail nebo volejte {options.Phone}.");
        text.AppendLine("Wake Park Džbán");

        static string H(string value) => WebUtility.HtmlEncode(value);

        StringBuilder html = new();
        html.Append("<!DOCTYPE html><html><body style=\"margin:0;padding:24px;background:#f5f5f7;font-family:Arial,sans-serif;color:#1d1d1f\">");
        html.Append("<div style=\"max-width:560px;margin:0 auto;background:#fff;border-radius:8px;padding:24px\">");
        html.Append("<h1 style=\"margin:0 0 12px;font-size:22px;color:#e50051\">").Append(H(subject)).Append("</h1>");
        html.Append("<p>").Append(H(en ? $"Hello {order.CustomerName}," : $"Dobrý den, {order.CustomerName},")).Append("</p>");
        html.Append("<p>").Append(H(intro)).Append("</p>");

        html.Append("<table style=\"width:100%;border-collapse:collapse;margin:16px 0\">");
        foreach (OrderLineDto line in order.Lines)
        {
            html.Append("<tr><td style=\"padding:6px 0;border-top:1px solid #eee\">")
                .Append(line.Quantity).Append(" × ").Append(H(line.Name))
                .Append("</td><td style=\"padding:6px 0;border-top:1px solid #eee;text-align:right;white-space:nowrap\">")
                .Append(H(Price(line.LineGross))).Append("</td></tr>");
        }

        html.Append("<tr><td style=\"padding:8px 0;border-top:2px solid #1d1d1f\"><b>").Append(en ? "Total" : "Celkem")
            .Append("</b></td><td style=\"padding:8px 0;border-top:2px solid #1d1d1f;text-align:right\"><b>")
            .Append(H(Price(order.TotalGross))).Append("</b></td></tr></table>");

        if (payment is not null)
        {
            html.Append("<h2 style=\"font-size:17px;margin:20px 0 8px\">").Append(en ? "How to pay" : "Jak zaplatit").Append("</h2>");
            html.Append("<p style=\"margin:0 0 12px\">").Append(H(en
                ? "Scan the QR code in your banking app, or enter the details below."
                : "Naskenujte QR kód v aplikaci své banky, nebo zadejte údaje níže.")).Append("</p>");
            html.Append("<img src=\"cid:").Append(QrContentId).Append("\" width=\"200\" height=\"200\" alt=\"QR\" style=\"display:block;margin:0 0 12px\">");
            html.Append("<table style=\"border-collapse:collapse\">");
            AppendRow(html, en ? "Account" : "Číslo účtu", payment.AccountNumber);
            AppendRow(html, "IBAN", payment.Iban);
            AppendRow(html, en ? "Variable symbol" : "Variabilní symbol", payment.VariableSymbol);
            AppendRow(html, en ? "Amount" : "Částka", Price(payment.Amount));
            AppendRow(html, en ? "Pay by" : "Zaplaťte do", due);
            html.Append("</table>");
        }

        html.Append("<p style=\"margin:20px 0\"><a href=\"").Append(H(orderUrl.ToString()))
            .Append("\" style=\"display:inline-block;padding:12px 18px;background:#e50051;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold\">")
            .Append(en ? "Order status" : "Stav objednávky").Append("</a></p>");
        html.Append("<p style=\"color:#6b6b72;font-size:13px\">").Append(H(en
            ? $"Questions? Reply to this e-mail or call {options.Phone}."
            : $"Dotazy? Odpovězte na tento e-mail nebo volejte {options.Phone}.")).Append("<br>Wake Park Džbán</p>");
        html.Append("</div></body></html>");

        return (subject, html.ToString(), text.ToString());
    }

    private static void AppendRow(StringBuilder html, string label, string value) =>
        html.Append("<tr><td style=\"padding:3px 16px 3px 0;color:#6b6b72\">").Append(WebUtility.HtmlEncode(label))
            .Append("</td><td style=\"padding:3px 0;font-weight:bold\">").Append(WebUtility.HtmlEncode(value)).Append("</td></tr>");
}
