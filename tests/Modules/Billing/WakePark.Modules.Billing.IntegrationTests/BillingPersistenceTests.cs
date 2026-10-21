using Microsoft.EntityFrameworkCore;

using Shouldly;

using Testcontainers.PostgreSql;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Billing.Domain.Customers;
using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Billing.Infrastructure.Persistence;
using WakePark.Modules.Billing.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Billing.IntegrationTests;

/// <summary>Číselná řada a uložení objednávky proti skutečnému PostgreSQL.</summary>
public sealed class BillingPersistenceTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using BillingDbContext dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task Rada_zacina_na_1000_a_kazdy_rok_znovu()
    {
        (await NextAsync(2026)).ShouldBe(1000);
        (await NextAsync(2026)).ShouldBe(1001);
        (await NextAsync(2027)).ShouldBe(1000);
    }

    [Fact]
    public async Task Soubezne_objednavky_nedostanou_stejne_cislo()
    {
        long[] numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => NextAsync(2026)));

        numbers.Distinct().Count().ShouldBe(20);
        numbers.Order().ShouldBe(Enumerable.Range(1000, 20).Select(n => (long)n));
    }

    [Fact]
    public async Task Objednavka_se_ulozi_a_nacte_podle_tokenu_i_s_kontaktem_a_radky()
    {
        string token = new('t', Order.AccessTokenLength);
        Order order = Order.Place(
            EnterpriseId,
            261000,
            token,
            CustomerContact.Create("Jana Nováková", "jana@example.cz", "+420 777 123 456", "Wake Park s.r.o.", "06633633", "CZ06633633"),
            "cs",
            "CZK",
            [new OrderLineDraft(Guid.CreateVersion7(), "voucher-2-sloty", "Voucher pro dva", "Voucher pro dva", 2, 1290m, 12m)],
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(4));

        await using (BillingDbContext dbContext = CreateDbContext())
        {
            new OrderRepository(dbContext).Add(order);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using BillingDbContext readContext = CreateDbContext();
        Order? loaded = await new OrderRepository(readContext).GetByAccessTokenAsync(token, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Number.ShouldBe(261000);
        loaded.Customer.CompanyId.ShouldBe("06633633");
        loaded.Lines.Single().LineGross.ShouldBe(2580m);
        loaded.TotalVat.ShouldBe(276.43m);
    }

    private async Task<long> NextAsync(int year)
    {
        await using BillingDbContext dbContext = CreateDbContext();
        return await new OrderNumberAllocator(dbContext).NextSequenceAsync(EnterpriseId, year, TestContext.Current.CancellationToken);
    }

    private BillingDbContext CreateDbContext()
    {
        DbContextOptionsBuilder<BillingDbContext> builder = new();
        builder.UseWakeParkNpgsql(_container.GetConnectionString(), BillingDbContext.Schema);
        return new BillingDbContext(builder.Options);
    }
}
