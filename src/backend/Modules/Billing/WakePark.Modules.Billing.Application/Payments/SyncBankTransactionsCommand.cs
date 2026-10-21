using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Domain.Invoices;
using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Billing.Domain.Payments;

namespace WakePark.Modules.Billing.Application.Payments;

/// <summary>
/// Stáhne příchozí pohyby za období, nové uloží do inboxu a spáruje vše, co čeká.
/// Bezpečné pustit kdykoli a kolikrát: známé pohyby se přeskočí, zpracované se nepárují znovu.
/// </summary>
public sealed record SyncBankTransactionsCommand(Guid EnterpriseId, DateOnly FirstDay, DateOnly LastDay)
    : ICommand<BankSyncResult>;

public sealed record BankSyncResult(int Downloaded, int New, int Matched, int Unmatched, int Ignored)
{
    public override string ToString() =>
        $"staženo {Downloaded}, nových {New}, spárováno {Matched}, k ručnímu vyřízení {Unmatched}, mimo e-shop {Ignored}";
}

internal sealed class SyncBankTransactionsCommandHandler(
    IBankStatementSource bank,
    IBankTransactionRepository transactions,
    IOrderRepository orders,
    IInvoiceRepository invoices,
    IInvoiceNumberAllocator invoiceNumbers,
    IBillingUnitOfWork unitOfWork,
    IClock clock,
    BillingOptions options)
    : ICommandHandler<SyncBankTransactionsCommand, BankSyncResult>
{
    public async Task<BankSyncResult> HandleAsync(SyncBankTransactionsCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        IReadOnlyList<BankTransactionData> downloaded = await bank
            .GetIncomingAsync(command.FirstDay, command.LastDay, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlySet<string> known = await transactions
            .GetKnownExternalIdsAsync(command.EnterpriseId, [.. downloaded.Select(d => d.ExternalId)], cancellationToken)
            .ConfigureAwait(false);

        int added = 0;
        foreach (BankTransactionData data in downloaded.Where(d => !known.Contains(d.ExternalId)).DistinctBy(d => d.ExternalId))
        {
            transactions.Add(BankTransaction.Receive(command.EnterpriseId, data));
            added++;
        }

        // Nejdřív uložit inbox. Kdyby párování spadlo, pohyby už jsou doma a příště se spárují.
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        int matched = 0, unmatched = 0, ignored = 0;
        DateTimeOffset now = clock.UtcNow;
        int year = command.LastDay.Year;
        DateOnly today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId)).DateTime);

        // Zaplacení objednávky, spárování pohybu i vystavení daňového dokladu v jedné transakci:
        // zaplacená objednávka bez dokladu (nebo doklad bez platby) nevznikne, a číslo dokladu
        // se při chybě vrátí do řady — řada zůstane bez mezer.
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
        foreach (BankTransaction transaction in await transactions
                     .GetReceivedAsync(command.EnterpriseId, ct)
                     .ConfigureAwait(false))
        {
            Order? order = PaymentMatching.OrderNumberFrom(transaction.VariableSymbol, year) is { } number
                ? await orders.GetByNumberAsync(command.EnterpriseId, number, ct).ConfigureAwait(false)
                : null;

            PaymentMatching.Decision decision = PaymentMatching.Evaluate(transaction, order, year);

            switch (decision.Outcome)
            {
                case PaymentMatching.Outcome.Matched:
                    order!.MarkPaid(now, transaction.Id);
                    transaction.MarkMatched(order.Id, now);
                    invoices.Add(Invoice.Issue(
                        order,
                        Seller(options.Seller),
                        today.Year,
                        await invoiceNumbers.NextSequenceAsync(command.EnterpriseId, today.Year, ct).ConfigureAwait(false),
                        // Kolem půlnoci může banka zaúčtovat „zítra“ — doklad nesmí být vystaven před DUZP.
                        issuedOn: transaction.BookedOn > today ? transaction.BookedOn : today,
                        taxableSupplyOn: transaction.BookedOn));
                    matched++;
                    break;
                case PaymentMatching.Outcome.Unmatched:
                    transaction.MarkUnmatched(decision.Reason!, now, order?.Id);
                    unmatched++;
                    break;
                default:
                    transaction.MarkIgnored(decision.Reason!, now);
                    ignored++;
                    break;
            }
        }
        }, cancellationToken).ConfigureAwait(false);

        return new BankSyncResult(downloaded.Count, added, matched, unmatched, ignored);
    }

    private static InvoiceParty Seller(SellerOptions seller) =>
        new(seller.Name, seller.CompanyId, seller.VatId, seller.Address, seller.Registration);
}
