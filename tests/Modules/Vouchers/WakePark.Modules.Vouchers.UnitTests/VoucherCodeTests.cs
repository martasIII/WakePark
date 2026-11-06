using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Vouchers.Application;
using WakePark.Modules.Vouchers.Application.Abstractions;
using WakePark.Modules.Vouchers.Domain;

namespace WakePark.Modules.Vouchers.UnitTests;

public sealed class VoucherCodeTests
{
    private const string RideCodePattern = "^V40-2027-[ABCDEFGHJKMNPQRSTUVWXYZ]{5}$";

    [Fact]
    public void Kod_jizdniho_poukazu_ma_typ_rok_sezony_a_5_velkych_pismen()
    {
        for (int i = 0; i < 500; i++)
        {
            Voucher.NewRideCode("40", 2027).ShouldMatch(RideCodePattern);
        }
    }

    [Fact]
    public void Nahodna_cast_nema_zamenitelna_pismena()
    {
        Voucher.RideCodeAlphabet.ShouldNotContain('I');
        Voucher.RideCodeAlphabet.ShouldNotContain('L');
        Voucher.RideCodeAlphabet.ShouldNotContain('O');
        Voucher.RideCodeAlphabet.Length.ShouldBe(23);
    }

    [Fact]
    public async Task Jizdni_poukaz_dostane_typ_z_nastaveni_ostatni_puvodni_kod()
    {
        Guid orderId = Guid.CreateVersion7();
        IOrderDirectory orders = Substitute.For<IOrderDirectory>();
        orders.GetOrderAsync("token", Arg.Any<CancellationToken>()).Returns(new OrderDto(
            261001, "Paid", "cs", "Jan Novák", "jan@example.cz", null,
            [
                new OrderLineDto("voucher-2-sloty", "Voucher pro dva", 2, 1290m, 12m, 2580m, Guid.CreateVersion7()),
                new OrderLineDto("permanentka-10x20", "Permanentka", 1, 4200m, 12m, 4200m, Guid.CreateVersion7()),
            ],
            6780m, 726.43m, 6053.57m, "CZK", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));

        List<Voucher> issued = [];
        IVoucherRepository vouchers = Substitute.For<IVoucherRepository>();
        vouchers.Add(Arg.Do<Voucher>(issued.Add));
        IClock clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));

        VouchersOptions options = new()
        {
            RideCodeTypes = new Dictionary<string, string> { ["voucher-2-sloty"] = "40" },
            ValidFrom = new DateOnly(2027, 5, 1),
            ValidTo = new DateOnly(2027, 9, 30),
        };

        OrderPaidConsumer consumer = new(
            orders, vouchers, Substitute.For<IVouchersOutbox>(), Substitute.For<IVouchersUnitOfWork>(), clock, options);
        await consumer.HandleAsync(
            new OrderPaidIntegrationEvent(Guid.CreateVersion7(), clock.UtcNow, Guid.CreateVersion7(), orderId, "token"),
            TestContext.Current.CancellationToken);

        issued.Count.ShouldBe(3);
        issued.Where(v => v.ProductCode == "voucher-2-sloty").ShouldAllBe(v => v.Code.StartsWith("V40-2027-", StringComparison.Ordinal));
        issued.Where(v => v.ProductCode == "voucher-2-sloty").Select(v => v.Code).Distinct().Count().ShouldBe(2);
        issued.Single(v => v.ProductCode == "permanentka-10x20").Code.ShouldMatch("^WD-[A-Z2-9]{4}-[A-Z2-9]{4}$");
    }
}
