using System.Globalization;
using System.Text;

namespace WakePark.Modules.Billing.Application.Payments;

/// <summary>
/// Obsah QR kódu pro platbu ve formátu SPAYD (Short Payment Descriptor, standard ČBA),
/// např. <c>SPD*1.0*ACC:CZ95…+FIOBCZPPXXX*AM:1290.00*CC:CZK*X-VS:261000*MSG:…</c>.
/// Tenhle formát čtou mobilní aplikace všech českých bank.
/// </summary>
public static class Spayd
{
    public const int MessageMaxLength = 60;

    public static string Build(string iban, string? bic, decimal amount, string currency, string variableSymbol, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iban);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(variableSymbol);

        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Částka musí být kladná.");
        }

        if (variableSymbol.Length > 10 || !variableSymbol.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Variabilní symbol má nejvýš 10 číslic.", nameof(variableSymbol));
        }

        string account = Clean(iban).ToUpperInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(bic))
        {
            account += "+" + Clean(bic).ToUpperInvariant();
        }

        StringBuilder spayd = new("SPD*1.0");
        spayd.Append("*ACC:").Append(account);
        spayd.Append("*AM:").Append(amount.ToString("0.00", CultureInfo.InvariantCulture));
        spayd.Append("*CC:").Append(currency.ToUpperInvariant());
        spayd.Append("*X-VS:").Append(variableSymbol);

        string cleanMessage = Message(message);
        if (cleanMessage.Length > 0)
        {
            spayd.Append("*MSG:").Append(cleanMessage);
        }

        return spayd.ToString();
    }

    /// <summary>
    /// Zpráva pro příjemce bez diakritiky a bez hvězdičky (oddělovač polí). Diakritiku
    /// standard dovoluje, ale některé banky ji ve výpisu rozbijí.
    /// </summary>
    public static string Message(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        string withoutDiacritics = new(
            [.. message.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);

        string clean = Clean(withoutDiacritics).Trim();
        return clean.Length > MessageMaxLength ? clean[..MessageMaxLength] : clean;
    }

    private static string Clean(string value) => value.Replace("*", string.Empty, StringComparison.Ordinal);
}
