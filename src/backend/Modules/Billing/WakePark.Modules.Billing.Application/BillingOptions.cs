namespace WakePark.Modules.Billing.Application;

/// <summary>Nastavení modulu Billing (sekce <c>Billing</c> v appsettings).</summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Za kolik dní objednávka bez platby propadne (ADR 0006).</summary>
    public int PaymentDueDays { get; init; } = 4;

    /// <summary>Po kolika dnech bez platby se pošle upomínka (ADR 0006).</summary>
    public int PaymentReminderDays { get; init; } = 2;

    /// <summary>Časová zóna provozovatele — podle ní se pozná rok v čísle objednávky.</summary>
    public string TimeZoneId { get; init; } = "Europe/Prague";

    /// <summary>
    /// Účet, na který se platí. Zatím z konfigurace, ne z <c>Enterprise</c> — tabulka
    /// provozovatelů v produkci ještě není (ADR 0006).
    /// </summary>
    public PaymentAccountOptions PaymentAccount { get; init; } = new();

    /// <summary>Prodávající na daňovém dokladu. Zatím z konfigurace, později z <c>Enterprise</c>.</summary>
    public SellerOptions Seller { get; init; } = new();
}

public sealed class SellerOptions
{
    public string Name { get; init; } = string.Empty;

    /// <summary>IČO.</summary>
    public string CompanyId { get; init; } = string.Empty;

    /// <summary>DIČ.</summary>
    public string? VatId { get; init; }

    public string Address { get; init; } = string.Empty;

    /// <summary>Zápis v obchodním rejstříku.</summary>
    public string? Registration { get; init; }
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
