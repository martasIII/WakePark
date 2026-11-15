using Shouldly;

using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application;
using WakePark.Modules.Notifications.Application.Orders;
using WakePark.Modules.Vouchers.Contracts;

namespace WakePark.Modules.Notifications.UnitTests;

/// <summary>E-mail po zaplacení mluví o tom, co zákazník koupil: vouchery, permanentky, nebo obojí.</summary>
public sealed class VouchersIssuedEmailTests
{
    private static readonly NotificationsOptions Options = new()
    {
        PassTemplates = new Dictionary<string, string> { ["permanentka-10x20"] = "permice-10x20.pdf" },
    };

    [Theory]
    [InlineData(new[] { "voucher-1-slot" }, "cs", "Vaše vouchery — objednávka 261000", "Vouchery máte v příloze")]
    [InlineData(new[] { "permanentka-10x20" }, "cs", "Vaše permanentky — objednávka 261000", "Permanentky máte v příloze")]
    [InlineData(new[] { "voucher-1-slot", "permanentka-10x20" }, "cs", "Vaše vouchery a permanentky — objednávka 261000", "Vouchery a permanentky máte")]
    [InlineData(new[] { "permanentka-10x20" }, "en", "Your season passes — order 261000", "Your season passes are attached")]
    public void Predmet_a_uvod_podle_koupenych_poukazu(string[] products, string language, string subject, string intro)
    {
        (string actualSubject, _, string text) = VouchersIssuedEmail.Render(
            Order(language), [.. products.Select(Voucher)], null, new Uri("https://x.test/o"), Options);

        actualSubject.ShouldBe(subject);
        text.ShouldContain(intro);
    }

    private static VoucherDto Voucher(string productCode) => new(
        productCode.StartsWith("permanentka", StringComparison.Ordinal) ? "PD2027-10x20-ABCDE" : "V20-2027-ABCDE",
        261000, productCode, "Poukaz", "Poukaz", "cs", 990m, 1, new DateOnly(2027, 5, 1), new DateOnly(2027, 9, 30), "Issued", null);

    private static OrderDto Order(string language) => new(
        261000, "Paid", language, "Jana", "jana@example.cz", null,
        [new OrderLineDto("voucher-1-slot", "Voucher", 1, 990m, 12m, 990m, Guid.CreateVersion7(), [""])],
        990m, 106.07m, 883.93m, "CZK", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
}
