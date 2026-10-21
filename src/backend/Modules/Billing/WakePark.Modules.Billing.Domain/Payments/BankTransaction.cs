using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Billing.Domain.Payments;

/// <summary>
/// Příchozí pohyb na bankovním účtu — **inbox** plateb (ADR 0006). Každý pohyb se uloží
/// právě jednou (unikátní <see cref="ExternalId"/>) a teprve potom se páruje. Opakované
/// stažení výpisu tak nikdy nezaplatí objednávku dvakrát.
/// </summary>
public sealed class BankTransaction : AggregateRoot, ITenantOwned, IAuditable
{
    public const int ExternalIdMaxLength = 50;
    public const int TextMaxLength = 255;

    private BankTransaction()
    {
    }

    private BankTransaction(Guid id, Guid enterpriseId, BankTransactionData data)
        : base(id)
    {
        EnterpriseId = enterpriseId;
        ExternalId = data.ExternalId;
        BookedOn = data.BookedOn;
        Amount = data.Amount;
        Currency = data.Currency;
        VariableSymbol = data.VariableSymbol;
        CounterpartyAccount = Truncate(data.CounterpartyAccount);
        CounterpartyName = Truncate(data.CounterpartyName);
        Message = Truncate(data.Message);
        Status = BankTransactionStatus.Received;
    }

    public Guid EnterpriseId { get; private set; }

    /// <summary>Id pohybu v bance (u Fio „ID pohybu“). Podle něj se pozná, že pohyb už známe.</summary>
    public string ExternalId { get; private set; } = string.Empty;

    public DateOnly BookedOn { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string? VariableSymbol { get; private set; }

    public string? CounterpartyAccount { get; private set; }

    public string? CounterpartyName { get; private set; }

    public string? Message { get; private set; }

    public BankTransactionStatus Status { get; private set; }

    /// <summary>Objednávka, kterou pohyb zaplatil — nebo ke které se vztahuje, ale nesedí.</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>Proč se pohyb nespároval — pro ruční vyřízení.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static BankTransaction Receive(Guid enterpriseId, BankTransactionData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (enterpriseId == Guid.Empty)
        {
            throw new DomainException("bank_transaction.enterprise_required", "Pohyb musí patřit provozovateli.");
        }

        if (string.IsNullOrWhiteSpace(data.ExternalId) || data.ExternalId.Length > ExternalIdMaxLength)
        {
            throw new DomainException("bank_transaction.invalid_id", "Pohyb musí mít Id z banky.");
        }

        return new BankTransaction(NewId(), enterpriseId, data);
    }

    public void MarkMatched(Guid orderId, DateTimeOffset nowUtc) =>
        Process(BankTransactionStatus.Matched, orderId, null, nowUtc);

    public void MarkUnmatched(string reason, DateTimeOffset nowUtc, Guid? orderId = null) =>
        Process(BankTransactionStatus.Unmatched, orderId, reason, nowUtc);

    /// <summary>
    /// Pohyb se e-shopu netýká. Na účet chodí i platby od lidí, kteří s e-shopem nemají nic
    /// společného — jejich jméno, účet a zprávu si nenecháváme. Zůstane jen to, co je potřeba,
    /// aby se pohyb při dalším stažení výpisu nezpracoval znovu.
    /// </summary>
    public void MarkIgnored(string reason, DateTimeOffset nowUtc)
    {
        Process(BankTransactionStatus.Ignored, null, reason, nowUtc);
        CounterpartyAccount = null;
        CounterpartyName = null;
        Message = null;
    }

    private void Process(BankTransactionStatus status, Guid? orderId, string? note, DateTimeOffset nowUtc)
    {
        if (Status != BankTransactionStatus.Received)
        {
            throw new DomainException("bank_transaction.already_processed", "Pohyb už byl zpracován.");
        }

        Status = status;
        OrderId = orderId;
        Note = Truncate(note);
        ProcessedAtUtc = nowUtc;
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length > TextMaxLength ? trimmed[..TextMaxLength] : trimmed;
    }
}

/// <summary>Pohyb tak, jak přišel z banky.</summary>
public sealed record BankTransactionData(
    string ExternalId,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? VariableSymbol,
    string? CounterpartyAccount,
    string? CounterpartyName,
    string? Message);

public enum BankTransactionStatus
{
    /// <summary>Uložen, čeká na párování.</summary>
    Received,

    /// <summary>Zaplatil objednávku.</summary>
    Matched,

    /// <summary>Vypadá jako platba za objednávku, ale nesedí — k ručnímu vyřízení.</summary>
    Unmatched,

    /// <summary>Netýká se e-shopu (odchozí platba, jiný VS…).</summary>
    Ignored,
}
