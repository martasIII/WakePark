using WakePark.Modules.Billing.Domain.Payments;

namespace WakePark.Modules.Billing.Application.Abstractions;

public interface IBankTransactionRepository
{
    /// <summary>Které z daných Id pohybů už v inboxu jsou.</summary>
    Task<IReadOnlySet<string>> GetKnownExternalIdsAsync(
        Guid enterpriseId,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken = default);

    /// <summary>Pohyby, které čekají na párování.</summary>
    Task<IReadOnlyList<BankTransaction>> GetReceivedAsync(Guid enterpriseId, CancellationToken cancellationToken = default);

    void Add(BankTransaction transaction);
}

/// <summary>Zdroj příchozích pohybů z banky (Fio API).</summary>
public interface IBankStatementSource
{
    /// <summary>Příchozí pohyby zaúčtované v daném období (včetně obou dnů).</summary>
    Task<IReadOnlyList<BankTransactionData>> GetIncomingAsync(
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken = default);
}
