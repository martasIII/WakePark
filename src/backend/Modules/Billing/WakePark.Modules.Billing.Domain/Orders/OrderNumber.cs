using System.Globalization;

namespace WakePark.Modules.Billing.Domain.Orders;

/// <summary>
/// Číslo objednávky = dvojčíslí roku + pořadí v roce (od 1000): <c>261000</c>, <c>261001</c> …
/// Zároveň je to variabilní symbol, ten má nejvýš 10 číslic. Formát je prozatímní (ADR 0006).
/// </summary>
public static class OrderNumber
{
    public const int FirstSequence = 1000;

    public const long MaxVariableSymbol = 9_999_999_999;

    public static long Compose(int year, long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, FirstSequence);

        long number = long.Parse(
            string.Create(CultureInfo.InvariantCulture, $"{year % 100:00}{sequence}"),
            CultureInfo.InvariantCulture);

        return number <= MaxVariableSymbol
            ? number
            : throw new InvalidOperationException($"Číslo objednávky {number} se nevejde do variabilního symbolu.");
    }
}
