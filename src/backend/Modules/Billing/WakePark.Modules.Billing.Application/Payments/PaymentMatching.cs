using System.Globalization;

using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Billing.Domain.Payments;

namespace WakePark.Modules.Billing.Application.Payments;

/// <summary>
/// Pravidla párování platby s objednávkou (ADR 0006). Automaticky se páruje jen jistota:
/// VS = číslo objednávky a přesná částka. Všechno ostatní, co vypadá jako platba za
/// objednávku, jde k ručnímu vyřízení — chybné spárování by znamenalo voucher za cizí peníze.
/// </summary>
public static class PaymentMatching
{
    public enum Outcome
    {
        Matched,
        Unmatched,
        Ignored,
    }

    public sealed record Decision(Outcome Outcome, string? Reason = null);

    /// <summary>
    /// Číslo objednávky z variabilního symbolu, nebo null, když VS číslo objednávky být nemůže —
    /// pak se platba e-shopu netýká. Na účet chodí i jiné platby (faktury s VS jako
    /// <c>20260117</c>), a ty nesmí zahltit seznam k ručnímu vyřízení.
    /// </summary>
    /// <param name="variableSymbol">VS z bankovního pohybu.</param>
    /// <param name="currentYear">Letošní rok — objednávky jsou z letoška nebo loňska.</param>
    public static long? OrderNumberFrom(string? variableSymbol, int currentYear)
    {
        string digits = (variableSymbol ?? string.Empty).Trim().TrimStart('0');

        // Číslo objednávky = 2 číslice roku + pořadí od 1000 → 6 až 8 číslic
        // (do 999 999 objednávek za rok).
        if (digits.Length is < 6 or > 8 || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        int year = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        if (year != currentYear % 100 && year != (currentYear - 1) % 100)
        {
            return null;
        }

        long sequence = long.Parse(digits[2..], CultureInfo.InvariantCulture);
        return sequence >= OrderNumber.FirstSequence ? long.Parse(digits, CultureInfo.InvariantCulture) : null;
    }

    public static Decision Evaluate(BankTransaction transaction, Order? order, int currentYear)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Amount <= 0m)
        {
            return new Decision(Outcome.Ignored, "Odchozí pohyb.");
        }

        if (OrderNumberFrom(transaction.VariableSymbol, currentYear) is null)
        {
            return new Decision(Outcome.Ignored, "VS není číslo objednávky e-shopu.");
        }

        if (order is null)
        {
            return new Decision(Outcome.Unmatched, $"Objednávka s VS {transaction.VariableSymbol} neexistuje.");
        }

        if (order.Status == OrderStatus.Paid)
        {
            return new Decision(Outcome.Unmatched, $"Objednávka {order.Number} už je zaplacená — druhá platba, vrátit?");
        }

        if (!string.Equals(order.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return new Decision(Outcome.Unmatched, $"Platba v {transaction.Currency}, objednávka v {order.Currency}.");
        }

        if (transaction.Amount != order.TotalGross)
        {
            string kind = transaction.Amount < order.TotalGross ? "Nedoplatek" : "Přeplatek";
            return new Decision(
                Outcome.Unmatched,
                string.Create(CultureInfo.InvariantCulture, $"{kind}: přišlo {transaction.Amount:0.00}, objednávka {order.TotalGross:0.00}."));
        }

        return new Decision(Outcome.Matched);
    }
}
