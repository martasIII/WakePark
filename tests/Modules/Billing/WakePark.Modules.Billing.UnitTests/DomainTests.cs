using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Billing.Domain.Customers;
using WakePark.Modules.Billing.Domain.Orders;

namespace WakePark.Modules.Billing.UnitTests;

public sealed class DomainTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private static readonly string Token = new('a', Order.AccessTokenLength);
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("06633633", true)]   // Wake Park s.r.o.
    [InlineData("25596641", true)]
    [InlineData("06633634", false)]  // překlep v poslední číslici
    [InlineData("0663363", false)]
    [InlineData("0663363X", false)]
    public void ICO_se_kontroluje_kontrolni_cislici(string companyId, bool valid)
    {
        CustomerContact.IsValidCompanyId(companyId).ShouldBe(valid);
    }

    [Fact]
    public void Kontakt_se_ocisti_a_DIC_prevede_na_velka_pismena()
    {
        CustomerContact contact = CustomerContact.Create(
            " Jana Nováková ", "jana@example.cz", " ", "Wake Park s.r.o.", "066 33 633", "cz06633633");

        contact.Name.ShouldBe("Jana Nováková");
        contact.Phone.ShouldBeNull();
        contact.CompanyId.ShouldBe("06633633");
        contact.VatId.ShouldBe("CZ06633633");
    }

    [Theory]
    [InlineData("", "jana@example.cz", null, null)]
    [InlineData("Jana", "jana(at)example.cz", null, null)]
    [InlineData("Jana", "jana@example.cz", "06633634", "Firma")]
    [InlineData("Jana", "jana@example.cz", "06633633", null)]
    public void Neplatny_kontakt_neprojde(string name, string email, string? companyId, string? companyName)
    {
        Should.Throw<DomainException>(() => CustomerContact.Create(name, email, null, companyName, companyId));
    }

    [Fact]
    public void DPH_se_pocita_shora_a_zaklad_s_dani_da_presne_cenu()
    {
        // 1 290 Kč s 12 % DPH: daň = 1290 × 12 / 112 = 138,214… → 138,21
        Order order = Place(Line(1290m, quantity: 1, vatRate: 12m));

        OrderLine line = order.Lines.Single();
        line.LineVat.ShouldBe(138.21m);
        line.LineNet.ShouldBe(1151.79m);
        (line.LineNet + line.LineVat).ShouldBe(line.LineGross);
    }

    [Fact]
    public void Soucty_objednavky_jsou_soucty_radku()
    {
        Order order = Place(Line(690m, quantity: 2), Line(4200m, quantity: 1));

        order.TotalGross.ShouldBe(5580m);
        order.TotalVat.ShouldBe(order.Lines.Sum(l => l.LineVat));
        order.TotalNet.ShouldBe(order.TotalGross - order.TotalVat);
        order.Status.ShouldBe(OrderStatus.AwaitingPayment);
        order.PaymentDueAtUtc.ShouldBe(Now.AddDays(4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Order.MaxQuantityPerLine + 1)]
    public void Pocet_kusu_mimo_rozsah_neprojde(int quantity)
    {
        Should.Throw<DomainException>(() => Place(Line(690m, quantity)));
    }

    [Fact]
    public void Stejny_produkt_dvakrat_neprojde()
    {
        Guid productId = Guid.CreateVersion7();

        Should.Throw<DomainException>(() => Place(
            Line(690m, 1) with { ProductId = productId },
            Line(690m, 1) with { ProductId = productId }));
    }

    [Fact]
    public void Prazdna_objednavka_neprojde()
    {
        Should.Throw<DomainException>(() => Place());
    }

    [Theory]
    [InlineData(2026, 1000, 261000)]
    [InlineData(2026, 1001, 261001)]
    [InlineData(2027, 12345, 2712345)]
    public void Cislo_objednavky_je_rok_a_poradi(int year, long sequence, long expected)
    {
        OrderNumber.Compose(year, sequence).ShouldBe(expected);
    }

    private static Order Place(params OrderLineDraft[] lines) =>
        Order.Place(
            EnterpriseId,
            261000,
            Token,
            CustomerContact.Create("Jana", "jana@example.cz"),
            "cs",
            "CZK",
            lines,
            Now,
            TimeSpan.FromDays(4));

    private static OrderLineDraft Line(decimal price, int quantity, decimal vatRate = 12m) =>
        new(Guid.CreateVersion7(), "voucher", "Voucher", "Voucher", quantity, price, vatRate);
}
