using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Billing.Application;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Application.Orders;
using WakePark.Modules.Billing.Domain.Customers;
using WakePark.Modules.Billing.Domain.Orders;

namespace WakePark.Modules.Billing.UnitTests;

public sealed class UnpaidOrdersTests
{
    private static readonly DateTimeOffset Placed = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Po_dvou_dnech_jedna_upominka_po_splatnosti_zruseni()
    {
        Order order = PlaceOrder();

        (await RunAsync(order, Placed.AddDays(1))).ShouldBe(new UnpaidOrdersResult(0, 0), "den po objednávce ještě nic");
        (await RunAsync(order, Placed.AddDays(2).AddMinutes(1))).ShouldBe(new UnpaidOrdersResult(1, 0));
        (await RunAsync(order, Placed.AddDays(3))).ShouldBe(new UnpaidOrdersResult(0, 0), "upomínka jen jednou");
        (await RunAsync(order, Placed.AddDays(4).AddMinutes(1))).ShouldBe(new UnpaidOrdersResult(0, 1));

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.DomainEvents.OfType<OrderPaymentReminded>().Count().ShouldBe(1);
        order.DomainEvents.OfType<OrderCancelled>().Count().ShouldBe(1);
    }

    [Fact]
    public void Zrusenou_objednavku_pozdni_platba_porad_zaplati()
    {
        Order order = PlaceOrder();
        order.CancelUnpaid(Placed.AddDays(5)).ShouldBeTrue();

        order.MarkPaid(Placed.AddDays(6), Guid.CreateVersion7());

        order.Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public void Zaplacena_objednavka_se_neupomina_ani_nerusi()
    {
        Order order = PlaceOrder();
        order.MarkPaid(Placed.AddHours(1), Guid.CreateVersion7());

        order.RemindPayment(Placed.AddDays(3)).ShouldBeFalse();
        order.CancelUnpaid(Placed.AddDays(10)).ShouldBeFalse();
    }

    private static async Task<UnpaidOrdersResult> RunAsync(Order order, DateTimeOffset now)
    {
        IOrderRepository orders = Substitute.For<IOrderRepository>();
        orders.GetAwaitingPaymentPlacedBeforeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => order.Status == OrderStatus.AwaitingPayment && order.PlacedAtUtc <= call.Arg<DateTimeOffset>()
                ? [order]
                : []);
        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);

        return await new ProcessUnpaidOrdersCommandHandler(orders, Substitute.For<IBillingUnitOfWork>(), clock, new BillingOptions())
            .HandleAsync(new ProcessUnpaidOrdersCommand(), TestContext.Current.CancellationToken);
    }

    private static Order PlaceOrder() =>
        Order.Place(
            Guid.CreateVersion7(),
            261000,
            new string('a', Order.AccessTokenLength),
            CustomerContact.Create("Jana", "jana@example.cz"),
            "cs",
            "CZK",
            [new OrderLineDraft(Guid.CreateVersion7(), "voucher", "Voucher", "Voucher", 1, 690m, 12m)],
            Placed,
            TimeSpan.FromDays(4));
}
