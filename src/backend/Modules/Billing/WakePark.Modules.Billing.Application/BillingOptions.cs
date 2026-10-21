namespace WakePark.Modules.Billing.Application;

/// <summary>Nastavení modulu Billing (sekce <c>Billing</c> v appsettings).</summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Za kolik dní objednávka bez platby propadne (ADR 0006).</summary>
    public int PaymentDueDays { get; init; } = 4;

    /// <summary>Časová zóna provozovatele — podle ní se pozná rok v čísle objednávky.</summary>
    public string TimeZoneId { get; init; } = "Europe/Prague";

    /// <summary>
    /// Účet, na který se platí. Zatím z konfigurace, ne z <c>Enterprise</c> — tabulka
    /// provozovatelů v produkci ještě není (ADR 0006).
    /// </summary>
    public PaymentAccountOptions PaymentAccount { get; init; } = new();
}

public sealed class PaymentAccountOptions
{
    /// <summary>Číslo účtu v tuzemském tvaru, pro ruční zadání platby.</summary>
    public string AccountNumber { get; init; } = string.Empty;

    public string Iban { get; init; } = string.Empty;

    public string? Bic { get; init; }

    /// <summary>Text, který se objeví u platby ve výpisu; za něj se doplní číslo objednávky.</summary>
    public string MessagePrefix { get; init; } = "Wake Park objednavka";
}
