using NSubstitute;

using Shouldly;

using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Application.Orders;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.UnitTests;

public sealed class OrderPlacedEmailTests
{
    private const string Token = "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";
    private static readonly TimeZoneInfo Prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

    [Fact]
    public void Cesky_email_ma_platebni_udaje_QR_a_odkaz_na_objednavku()
    {
        (string subject, string html, string text) = OrderPlacedEmail.Render(Order("cs"), new Uri($"https://novy.wake-dzban.cz/objednavka/{Token}"), new NotificationsOptions(), Prague);

        subject.ShouldBe("Objednávka 261000 — Wake Park Džbán");
        html.ShouldContain("cid:qr-platba");
        html.ShouldContain("2601338268/2010");
        html.ShouldContain($"https://novy.wake-dzban.cz/objednavka/{Token}");
        // Cena s nezlomitelnými mezerami; HTML je kóduje jako entity.
        text.ShouldContain("1 290 Kč");
        System.Net.WebUtility.HtmlDecode(html).ShouldContain("1 290 Kč");
        text.ShouldContain("Variabilní symbol: 261000");
        // Splatnost v pražském čase: 25. 10. 18:00 UTC = 19:00 SEČ.
        text.ShouldContain("25. 10. 2026 19:00");
    }

    [Fact]
    public void Anglicky_email_je_anglicky()
    {
        (string subject, _, string text) = OrderPlacedEmail.Render(Order("en"), new Uri("https://x.test/en/objednavka/t"), new NotificationsOptions(), Prague);

        subject.ShouldBe("Order 261000 — Wake Park Džbán");
        text.ShouldContain("Variable symbol: 261000");
        text.ShouldContain("Hello Jana");
    }

    [Fact]
    public void Jmeno_zakaznika_se_v_HTML_koduje()
    {
        OrderDto order = Order("cs") with { CustomerName = "<script>alert(1)</script>" };

        (_, string html, _) = OrderPlacedEmail.Render(order, new Uri("https://x.test/o"), new NotificationsOptions(), Prague);

        html.ShouldNotContain("<script>");
        html.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public async Task Opakovane_doruceni_udalosti_email_nezdvoji()
    {
        IOrderDirectory orders = Substitute.For<IOrderDirectory>();
        orders.GetOrderAsync(Token, Arg.Any<CancellationToken>()).Returns(Order("cs"));
        IEmailRepository emails = Substitute.For<IEmailRepository>();
        IQrCodeImage qr = Substitute.For<IQrCodeImage>();
        qr.Png(Arg.Any<string>()).Returns([1, 2, 3]);

        OrderPlacedIntegrationEvent placed = new(Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.CreateVersion7(), Token);
        OrderPlacedConsumer consumer = new(orders, emails, Substitute.For<INotificationsUnitOfWork>(), qr, new NotificationsOptions());

        await consumer.HandleAsync(placed, TestContext.Current.CancellationToken);
        emails.ExistsAsync(placed.EventId, OrderPlacedEmail.Kind, Arg.Any<CancellationToken>()).Returns(true);
        await consumer.HandleAsync(placed, TestContext.Current.CancellationToken);

        emails.Received(1).Add(Arg.Is<EmailMessage>(m =>
            m.To == "jana@example.cz"
            && m.Attachments.Single().ContentId == OrderPlacedEmail.QrContentId));
    }

    [Fact]
    public void Neodeslany_email_se_zkousi_s_rostoucim_odstupem_a_nakonec_vzda()
    {
        EmailMessage message = EmailMessage.Create(Guid.CreateVersion7(), "order-placed", "jana@example.cz", "Předmět", "<p>x</p>", "x");
        DateTimeOffset now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

        message.MarkFailed("SMTP nedostupné", now);
        message.NextAttemptAtUtc.ShouldBe(now.AddMinutes(1));
        message.MarkFailed("SMTP nedostupné", now);
        message.NextAttemptAtUtc.ShouldBe(now.AddMinutes(2));

        for (int i = 2; i < EmailMessage.MaxAttempts; i++)
        {
            message.MarkFailed("SMTP nedostupné", now);
        }

        message.Status.ShouldBe(EmailStatus.Failed);
    }

    private static OrderDto Order(string language) => new(
        261000,
        "AwaitingPayment",
        language,
        "Jana",
        "jana@example.cz",
        null,
        [new OrderLineDto("voucher-2-sloty", "Voucher pro dva", 1, 1290m, 12m, 1290m)],
        1290m,
        138.21m,
        1151.79m,
        "CZK",
        new DateTimeOffset(2026, 10, 21, 18, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 10, 25, 18, 0, 0, TimeSpan.Zero),
        new PaymentInstructionsDto("2601338268/2010", "CZ9520100000002601338268", "FIOBCZPPXXX", "261000", 1290m, "CZK", "Wake Park objednavka 261000", "SPD*1.0*ACC:CZ9520100000002601338268*AM:1290.00*CC:CZK*X-VS:261000"));
}
