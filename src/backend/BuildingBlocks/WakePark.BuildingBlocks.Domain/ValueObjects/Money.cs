using System.Globalization;

namespace WakePark.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Peněžní částka s měnou. Součet částek v různých měnách je chyba, ne implicitní konverze.
/// </summary>
public sealed class Money : ValueObject
{
    private Money()
    {
    }

    public Money(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (currency.Length != 3)
        {
            throw new ArgumentException("Měna musí být třípísmenný kód podle ISO 4217.", nameof(currency));
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; private init; }

    public string Currency { get; private init; } = string.Empty;

    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Nelze počítat s částkami v různých měnách ({Currency} vs. {other.Currency}).");
        }
    }
}
