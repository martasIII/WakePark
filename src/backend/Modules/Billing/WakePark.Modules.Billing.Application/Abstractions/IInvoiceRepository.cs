using WakePark.Modules.Billing.Domain.Invoices;

namespace WakePark.Modules.Billing.Application.Abstractions;

public interface IInvoiceRepository
{
    void Add(Invoice invoice);

    Task<Invoice?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Přiděluje čísla daňových dokladů — řada **bez mezer**. Musí se volat uvnitř transakce
/// <see cref="IBillingUnitOfWork.ExecuteInTransactionAsync"/>: čítač zůstane zamčený až do
/// potvrzení, a když se doklad neuloží, vrátí se i číslo.
/// </summary>
public interface IInvoiceNumberAllocator
{
    Task<long> NextSequenceAsync(Guid enterpriseId, int year, CancellationToken cancellationToken = default);
}
