using Microsoft.EntityFrameworkCore;

using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Domain.Invoices;
using WakePark.Modules.Billing.Domain.Orders;

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(BillingDbContext dbContext) : IOrderRepository
{
    public void Add(Order order) => dbContext.Orders.Add(order);

    public Task<Order?> GetByAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default) =>
        dbContext.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.AccessToken == accessToken, cancellationToken);

    public Task<Order?> GetByNumberAsync(Guid enterpriseId, long number, CancellationToken cancellationToken = default) =>
        dbContext.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.EnterpriseId == enterpriseId && o.Number == number, cancellationToken);
}

/// <summary>
/// Přidělí další pořadí jedním příkazem: založí čítač pro nový rok, nebo ho zvýší.
/// Postgres při konfliktu zamkne řádek, takže dvě souběžné objednávky dostanou různá čísla
/// bez explicitní transakce a bez zámku v aplikaci.
/// </summary>
/// <remarks>
/// Běží mimo transakci ukládání objednávky. Když se objednávka pak neuloží, číslo propadne —
/// u objednávek mezery nevadí (u daňových dokladů by vadily, ty budou mít vlastní řadu).
/// </remarks>
internal sealed class OrderNumberAllocator(BillingDbContext dbContext) : IOrderNumberAllocator
{
    public async Task<long> NextSequenceAsync(Guid enterpriseId, int year, CancellationToken cancellationToken = default)
    {
        long first = OrderNumber.FirstSequence;

        List<long> allocated = await dbContext.Database
            .SqlQuery<long>($"""
                INSERT INTO billing.order_number_series (enterprise_id, year, last_sequence)
                VALUES ({enterpriseId}, {year}, {first})
                ON CONFLICT (enterprise_id, year)
                DO UPDATE SET last_sequence = order_number_series.last_sequence + 1
                RETURNING last_sequence AS "Value"
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return allocated.Single();
    }
}

/// <summary>
/// Číslo daňového dokladu. Stejný příkaz jako u objednávek, ale volá se uvnitř transakce:
/// řádek čítače zůstane zamčený do potvrzení (další doklad počká) a při chybě se změna
/// čítače vrátí spolu s dokladem — řada je bez mezer, jak to vyžaduje zákon o DPH.
/// </summary>
internal sealed class InvoiceNumberAllocator(BillingDbContext dbContext) : IInvoiceNumberAllocator
{
    public async Task<long> NextSequenceAsync(Guid enterpriseId, int year, CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Číslo daňového dokladu se smí přidělit jen uvnitř transakce.");
        }

        List<long> allocated = await dbContext.Database
            .SqlQuery<long>($"""
                INSERT INTO billing.invoice_number_series (enterprise_id, year, last_sequence)
                VALUES ({enterpriseId}, {year}, 1)
                ON CONFLICT (enterprise_id, year)
                DO UPDATE SET last_sequence = invoice_number_series.last_sequence + 1
                RETURNING last_sequence AS "Value"
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return allocated.Single();
    }
}

internal sealed class InvoiceRepository(BillingDbContext dbContext) : IInvoiceRepository
{
    public void Add(Invoice invoice) => dbContext.Invoices.Add(invoice);

    public Task<Invoice?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        dbContext.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.OrderId == orderId, cancellationToken);
}
