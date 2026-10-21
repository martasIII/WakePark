using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Billing.Domain.Orders;

namespace WakePark.Modules.Billing.Application.Abstractions;

public interface IOrderRepository
{
    void Add(Order order);

    Task<Order?> GetByAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Přiděluje pořadí v číselné řadě objednávek. Musí být atomické — dvě objednávky ve stejnou
/// chvíli nesmí dostat stejné číslo. Mezery (objednávka, která se nakonec neuloží) nevadí.
/// </summary>
public interface IOrderNumberAllocator
{
    Task<long> NextSequenceAsync(Guid enterpriseId, int year, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IUnitOfWork" />
/// <remarks>Vlastní rozhraní pro každý modul — sdílené by v jednom DI kontejneru přepsal poslední modul.</remarks>
public interface IBillingUnitOfWork : IUnitOfWork;
