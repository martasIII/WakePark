using FluentValidation;

using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Billing.Application;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Application.Orders;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Billing.UnitTests;

public sealed class PlaceOrderCommandHandlerTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly Guid VoucherId = Guid.CreateVersion7();

    private readonly ICatalogReader _catalog = Substitute.For<ICatalogReader>();
    private readonly IOrderNumberAllocator _numbers = Substitute.For<IOrderNumberAllocator>();
    private readonly List<Order> _saved = [];
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IBillingUnitOfWork _unitOfWork = Substitute.For<IBillingUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public PlaceOrderCommandHandlerTests()
    {
        _catalog.GetPublishedCatalogAsync(EnterpriseId, Arg.Any<CancellationToken>()).Returns(new CatalogDto(
            [],
            [
                new CatalogProductDto(
                    VoucherId,
                    "voucher-2-sloty",
                    "vouchery",
                    new Dictionary<string, string> { ["cs"] = "Voucher pro dva", ["en"] = "Voucher for two" },
                    new Dictionary<string, string> { ["cs"] = "Popis" },
                    null,
                    1290m,
                    "CZK",
                    12m),
            ]));

        _numbers.NextSequenceAsync(EnterpriseId, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(1000L);
        _orders.When(o => o.Add(Arg.Any<Order>())).Do(call => _saved.Add(call.Arg<Order>()));
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 21, 18, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Cena_a_nazev_se_berou_z_katalogu_a_zamrazi()
    {
        PlacedOrderDto placed = await PlaceAsync(Request(new PlaceOrderLine(VoucherId, 2), language: "en"));

        placed.Number.ShouldBe(261000);
        placed.AccessToken.Length.ShouldBe(Order.AccessTokenLength);

        OrderLine line = _saved.Single().Lines.Single();
        line.UnitPrice.ShouldBe(1290m);
        line.VatRate.ShouldBe(12m);
        line.Description.ShouldBe("Voucher pro dva", "doklad je vždy česky");
        line.DisplayName.ShouldBe("Voucher for two");
        _saved.Single().TotalGross.ShouldBe(2580m);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rok_v_cisle_se_bere_v_ceskem_case()
    {
        // 31. 12. 23:30 UTC je v Praze už 1. 1. — objednávka patří do nového roku.
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero));

        await PlaceAsync(Request(new PlaceOrderLine(VoucherId, 1)));

        await _numbers.Received(1).NextSequenceAsync(EnterpriseId, 2027, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bez_souhlasu_s_podminkami_objednavka_nevznikne()
    {
        ValidationException error = await Should.ThrowAsync<ValidationException>(() =>
            PlaceAsync(Request(new PlaceOrderLine(VoucherId, 1)) with { TermsAccepted = false }));

        error.Message.ShouldContain("obchodními podmínkami");
        _saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Produkt_ktery_uz_neni_v_nabidce_neprojde()
    {
        await Should.ThrowAsync<ValidationException>(() =>
            PlaceAsync(Request(new PlaceOrderLine(Guid.CreateVersion7(), 1))));

        await _numbers.DidNotReceiveWithAnyArgs().NextSequenceAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Neplatny_kontakt_nespotrebuje_cislo_z_rady()
    {
        PlaceOrderRequest request = Request(new PlaceOrderLine(VoucherId, 1)) with
        {
            Customer = new PlaceOrderCustomer("Jana", "neplatny-email", null, null, null, null),
        };

        await Should.ThrowAsync<DomainException>(() => PlaceAsync(request));

        await _numbers.DidNotReceiveWithAnyArgs().NextSequenceAsync(default, default, TestContext.Current.CancellationToken);
    }

    private Task<PlacedOrderDto> PlaceAsync(PlaceOrderRequest request) =>
        new PlaceOrderCommandHandler(_catalog, _numbers, _orders, _unitOfWork, _clock, new BillingOptions())
            .HandleAsync(new PlaceOrderCommand(EnterpriseId, request), TestContext.Current.CancellationToken);

    private static PlaceOrderRequest Request(PlaceOrderLine line, string language = "cs") =>
        new(language, new PlaceOrderCustomer("Jana Nováková", "jana@example.cz", null, null, null, null), [line], TermsAccepted: true);
}
