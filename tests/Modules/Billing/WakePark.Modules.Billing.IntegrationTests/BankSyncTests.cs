using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Shouldly;

using Testcontainers.PostgreSql;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Application.Payments;
using WakePark.Modules.Billing.Domain.Customers;
using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Billing.Domain.Payments;
using WakePark.Modules.Billing.Infrastructure.Persistence;
using WakePark.Modules.Billing.Infrastructure.Persistence.Repositories;

namespace WakePark.Modules.Billing.IntegrationTests;

/// <summary>
/// Párování plateb proti skutečnému PostgreSQL. Hlavně: výpis se stahuje pořád znovu za
/// několik dní zpětně, a přesto se žádná platba nezapočítá dvakrát.
/// </summary>
public sealed class BankSyncTests : IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 10, 22);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wakepark_test")
        .WithUsername("wakepark")
        .WithPassword("wakepark")
        .Build();

    private readonly List<BankTransactionData> _statement = [];

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using BillingDbContext dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task Platba_zaplati_objednavku_a_opakovane_stazeni_nic_nezmeni()
    {
        string token = await PlaceOrderAsync(261000, 2580m);
        _statement.Add(Payment("1001", "261000", 2580m));

        BankSyncResult first = await SyncAsync();
        BankSyncResult second = await SyncAsync();

        first.ShouldBe(new BankSyncResult(Downloaded: 1, New: 1, Matched: 1, Unmatched: 0, Ignored: 0));
        second.ShouldBe(new BankSyncResult(Downloaded: 1, New: 0, Matched: 0, Unmatched: 0, Ignored: 0));

        Order order = await LoadOrderAsync(token);
        order.Status.ShouldBe(OrderStatus.Paid);
        order.PaidByBankTransactionId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Nedoplatek_ani_cizi_platba_objednavku_nezaplati()
    {
        string token = await PlaceOrderAsync(261000, 2580m);
        _statement.Add(Payment("2001", "261000", 2500m));       // nedoplatek
        _statement.Add(Payment("2002", "20260117", 15000m));   // faktura od firmy, VS podle data
        _statement.Add(Payment("2003", null, 690m));           // bez VS

        BankSyncResult result = await SyncAsync();

        result.ShouldBe(new BankSyncResult(Downloaded: 3, New: 3, Matched: 0, Unmatched: 1, Ignored: 2));
        (await LoadOrderAsync(token)).Status.ShouldBe(OrderStatus.AwaitingPayment);

        await using BillingDbContext dbContext = CreateDbContext();
        BankTransaction underpaid = await dbContext.BankTransactions.SingleAsync(t => t.ExternalId == "2001", TestContext.Current.CancellationToken);
        underpaid.Status.ShouldBe(BankTransactionStatus.Unmatched);
        underpaid.Note.ShouldBe("Nedoplatek: přišlo 2500.00, objednávka 2580.00.");
        underpaid.CounterpartyName.ShouldBe("Nováková Jana", "k ručnímu vyřízení je potřeba vědět, komu peníze vrátit");

        BankTransaction invoice = await dbContext.BankTransactions.SingleAsync(t => t.ExternalId == "2002", TestContext.Current.CancellationToken);
        invoice.Status.ShouldBe(BankTransactionStatus.Ignored);
        invoice.CounterpartyName.ShouldBeNull("platby mimo e-shop si osobní údaje nenechávají");
        invoice.CounterpartyAccount.ShouldBeNull();
    }

    [Fact]
    public async Task Dve_platby_za_stejnou_objednavku_ve_stejnem_vypisu_zaplati_jen_jednou()
    {
        await PlaceOrderAsync(261000, 2580m);
        _statement.Add(Payment("3001", "261000", 2580m));
        _statement.Add(Payment("3002", "261000", 2580m));

        BankSyncResult result = await SyncAsync();

        result.Matched.ShouldBe(1);
        result.Unmatched.ShouldBe(1, "druhou platbu je potřeba vrátit — ručně");
    }

    [Fact]
    public async Task Stejny_pohyb_nejde_do_inboxu_ulozit_dvakrat_ani_mimo_aplikaci()
    {
        await using BillingDbContext dbContext = CreateDbContext();
        dbContext.BankTransactions.Add(BankTransaction.Receive(EnterpriseId, Payment("4001", "261000", 100m)));
        dbContext.BankTransactions.Add(BankTransaction.Receive(EnterpriseId, Payment("4001", "261000", 100m)));

        await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private async Task<BankSyncResult> SyncAsync()
    {
        IBankStatementSource bank = Substitute.For<IBankStatementSource>();
        bank.GetIncomingAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => _statement.ToList());

        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));

        await using BillingDbContext dbContext = CreateDbContext();
        return await new SyncBankTransactionsCommandHandler(
                bank, new BankTransactionRepository(dbContext), new OrderRepository(dbContext), dbContext, clock)
            .HandleAsync(new SyncBankTransactionsCommand(EnterpriseId, Today.AddDays(-7), Today), TestContext.Current.CancellationToken);
    }

    private async Task<string> PlaceOrderAsync(long number, decimal price)
    {
        string token = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()) + Convert.ToHexStringLower(Guid.NewGuid().ToByteArray());
        Order order = Order.Place(
            EnterpriseId,
            number,
            token,
            CustomerContact.Create("Jana", "jana@example.cz"),
            "cs",
            "CZK",
            [new OrderLineDraft(Guid.CreateVersion7(), "voucher", "Voucher", "Voucher", 1, price, 12m)],
            new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero),
            TimeSpan.FromDays(4));

        await using BillingDbContext dbContext = CreateDbContext();
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return token;
    }

    private async Task<Order> LoadOrderAsync(string token)
    {
        await using BillingDbContext dbContext = CreateDbContext();
        return (await new OrderRepository(dbContext).GetByAccessTokenAsync(token, TestContext.Current.CancellationToken))!;
    }

    private static BankTransactionData Payment(string id, string? variableSymbol, decimal amount) =>
        new(id, Today, amount, "CZK", variableSymbol, "1234567890/0800", "Nováková Jana", null);

    private BillingDbContext CreateDbContext()
    {
        DbContextOptionsBuilder<BillingDbContext> builder = new();
        builder.UseWakeParkNpgsql(_container.GetConnectionString(), BillingDbContext.Schema);
        return new BillingDbContext(builder.Options);
    }
}
