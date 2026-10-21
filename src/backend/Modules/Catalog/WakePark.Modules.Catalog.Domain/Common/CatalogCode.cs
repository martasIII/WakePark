using System.Text.RegularExpressions;

using WakePark.BuildingBlocks.Domain;

namespace WakePark.Modules.Catalog.Domain.Common;

/// <summary>
/// Stabilní kód produktu nebo kategorie, např. <c>voucher-2-sloty</c>. Podle něj se při importu
/// páruje záznam v souboru s řádkem v databázi, proto se nesmí měnit — změna kódu znamená
/// nový produkt a starý se skryje.
/// </summary>
public static partial class CatalogCode
{
    public const int MaxLength = 100;

    public static string Normalize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        string normalized = code.Trim();

        if (normalized.Length is 0 or > MaxLength || !Pattern().IsMatch(normalized))
        {
            throw new DomainException(
                "catalog.invalid_code",
                $"Kód '{code}' smí obsahovat jen malá písmena bez diakritiky, číslice a pomlčky.");
        }

        return normalized;
    }

    public static bool IsValid(string? code) =>
        code is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(code);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
