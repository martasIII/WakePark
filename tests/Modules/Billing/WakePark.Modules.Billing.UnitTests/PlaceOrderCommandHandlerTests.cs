using FluentValidation;

using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Billing.Application;
using WakePark.Modules.Billing.Application.Abstractions;
using WakePark.Modules.Billing.Application.Orders;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Billing.Domain.Customers;
using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Catalog.Contracts;

namespace WakePark.Modules.Billing.UnitTests;

public sealed class PlaceOrderCommandHandlerTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly Guid VoucherId = Guid.CreateVersion7();

    private readonly ICatalogReader _catalog = Substitute.For<ICatalogReader>();
    private readonly ICompanyRegistry _companies = Substitute.For<ICompanyRegistry>();
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
            Customer = new PlaceOrderCustomer("Jana", "neplatny-email", null, null),
        };

        await Should.ThrowAsync<DomainException>(() => PlaceAsync(request));

        await _numbers.DidNotReceiveWithAnyArgs().NextSequenceAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Firma_se_na_doklad_bere_z_ARES_ne_z_formulare()
    {
        _companies.FindAsync("06633633", Arg.Any<CancellationToken>()).Returns(
            new RegisteredCompany("06633633", "Wake Park s.r.o.", "CZ06633633", "Pavlovská 585/1, Liboc, 16200 Praha 6", null));

        await PlaceAsync(Request(new PlaceOrderLine(VoucherId, 1), companyId: "066 33 633"));

        CustomerContact customer = _saved.Single().Customer;
        customer.Name.ShouldBe("Jana Nováková", "objednatel zůstává z formuláře");
        customer.CompanyName.ShouldBe("Wake Park s.r.o.");
        customer.CompanyId.ShouldBe("06633633");
        customer.VatId.ShouldBe("CZ06633633");
        customer.CompanyAddress.ShouldBe("Pavlovská 585/1, Liboc, 16200 Praha 6");
    }

    [Fact]
    public async Task Neplatne_ICO_se_do_ARES_vubec_neposila()
    {
        await Should.ThrowAsync<DomainException>(() =>
            PlaceAsync(Request(new PlaceOrderLine(VoucherId, 1), companyId: "06633634")));

        await _companies.DidNotReceiveWithAnyArgs().FindAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Firma_kterou_ARES_nezna_neprojde_a_nespotrebuje_cislo()
    {
        _companies.FindAsync("06633633", Arg.Any<CancellationToken>()).Returns((RegisteredCompany?)null);

        ValidationException error = await Should.ThrowAsync<ValidationException>(() =>
            PlaceAsync(Request(new PlaceOrderLine(VoucherId, 1), companyId: "06633633")));

        error.Message.ShouldContain("nenašli");
        await _numbers.DidNotReceiveWithAnyArgs().NextSequenceAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Zanikla_firma_neprojde()
    {
        _companies.FindAsync("06633633", Arg.Any<CancellationToken>()).Returns(
            new RegisteredCompany("06633633", "Stará firma s.r.o.", null, null, new DateOnly(2020, 1, 1)));

        ValidationException error = await Should.ThrowAsync<ValidationException>(() =>
            PlaceAsync(Request(new PlaceOrderLine(VoucherId, 1), companyId: "06633633")));

        error.Message.ShouldContain("zanikla");
    }

    [Fact]
    public async Task Pri_vypadku_ARES_objednavka_na_firmu_neprojde_se_srozumitelnou_hlaskou()
    {
        _companies.FindAsync("06633633", Arg.Any<CancellationToken>())
            .Returns<RegisteredCompany?>(_ => throw new CompanyRegistryUnavailableException());

        ValidationException error = await Should.ThrowAsync<ValidationException>(() =>
            PlaceAsync(Request(new PlaceOrderLine(VoucherId, 1), companyId: "06633633")));

        error.Message.ShouldContain("neodpovídá");
        _saved.ShouldBeEmpty();
    }

    private Task<PlacedOrderDto> PlaceAsync(PlaceOrderRequest request) =>
        new PlaceOrderCommandHandler(_catalog, _companies, _numbers, _orders, _unitOfWork, _clock, new BillingOptions())
            .HandleAsync(new PlaceOrderCommand(EnterpriseId, request), TestContext.Current.CancellationToken);

    private static PlaceOrderRequest Request(PlaceOrderLine line, string language = "cs", string? companyId = null) =>
        new(language, new PlaceOrderCustomer("Jana Nováková", "jana@example.cz", null, companyId), [line], TermsAccepted: true);
}
