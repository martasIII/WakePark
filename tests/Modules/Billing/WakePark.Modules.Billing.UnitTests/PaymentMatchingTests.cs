using Shouldly;

using WakePark.Modules.Billing.Application.Payments;
using WakePark.Modules.Billing.Domain.Customers;
using WakePark.Modules.Billing.Domain.Orders;
using WakePark.Modules.Billing.Domain.Payments;

using static WakePark.Modules.Billing.Application.Payments.PaymentMatching;

namespace WakePark.Modules.Billing.UnitTests;

public sealed class PaymentMatchingTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();
    private const int Year = 2026;

    [Theory]
    [InlineData("261000", 261000L)]
    [InlineData("0000261000", 261000L)]   // některé banky doplní nuly zleva
    [InlineData(" 2612345 ", 2612345L)]
    [InlineData("251000", 251000L)]       // loňská objednávka zaplacená v lednu
    [InlineData("241000", null)]          // předloňská — tak stará objednávka být nemůže
    [InlineData("260999", null)]          // pořadí začíná od 1000
    [InlineData("20260117", null)]        // faktura s VS podle data — e-shopu se netýká
    [InlineData("12345", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("26A000", null)]
    public void VS_se_pozna_jako_cislo_objednavky(string? variableSymbol, long? expected)
    {
        OrderNumberFrom(variableSymbol, currentYear: 2026).ShouldBe(expected);
    }

    [Fact]
    public void Presna_castka_se_spravnym_VS_se_sparuje()
    {
        Evaluate(Payment("261000", 2580m), Order(2580m), Year).Outcome.ShouldBe(Outcome.Matched);
    }

    [Fact]
    public void Platba_po_splatnosti_se_prijme()
    {
        // Splatnost 4 dny, platba přišla za týden — poukazů není omezený počet (ADR 0006).
        Order late = Order(2580m);

        Evaluate(Payment("261000", 2580m), late, Year).Outcome.ShouldBe(Outcome.Matched);
    }

    [Theory]
    [InlineData(2500, "Nedoplatek")]
    [InlineData(2600, "Přeplatek")]
    public void Jina_castka_jde_k_rucnimu_vyrizeni(decimal amount, string reason)
    {
        Decision decision = Evaluate(Payment("261000", amount), Order(2580m), Year);

        decision.Outcome.ShouldBe(Outcome.Unmatched);
        decision.Reason!.ShouldStartWith(reason);
    }

    [Fact]
    public void VS_objednavky_ktera_neexistuje_jde_k_rucnimu_vyrizeni()
    {
        Evaluate(Payment("269999", 2580m), order: null, Year).Outcome.ShouldBe(Outcome.Unmatched);
    }

    [Fact]
    public void Druha_platba_za_zaplacenou_objednavku_jde_k_rucnimu_vyrizeni()
    {
        Order paid = Order(2580m);
        paid.MarkPaid(DateTimeOffset.UtcNow, Guid.CreateVersion7());

        Decision decision = Evaluate(Payment("261000", 2580m), paid, Year);

        decision.Outcome.ShouldBe(Outcome.Unmatched);
        decision.Reason!.ShouldContain("už je zaplacená");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData(null)]
    public void Platba_s_jinym_VS_se_e_shopu_netyka(string? variableSymbol)
    {
        Evaluate(Payment(variableSymbol, 2580m), order: null, Year).Outcome.ShouldBe(Outcome.Ignored);
    }

    private static BankTransaction Payment(string? variableSymbol, decimal amount) =>
        BankTransaction.Receive(EnterpriseId, new BankTransactionData(
            Guid.NewGuid().ToString(), new DateOnly(2026, 10, 22), amount, "CZK", variableSymbol, "123/0100", "Jana", null));

    private static Order Order(decimal price) =>
        Domain.Orders.Order.Place(
            EnterpriseId,
            261000,
            new string('a', Domain.Orders.Order.AccessTokenLength),
            CustomerContact.Create("Jana", "jana@example.cz"),
            "cs",
            "CZK",
            [new OrderLineDraft(Guid.CreateVersion7(), "voucher", "Voucher", "Voucher", 1, price, 12m)],
            new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero),
            TimeSpan.FromDays(4));
}
