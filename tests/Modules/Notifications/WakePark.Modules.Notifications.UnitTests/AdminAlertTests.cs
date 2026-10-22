using NSubstitute;

using Shouldly;

using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Billing.Contracts;
using WakePark.Modules.Notifications.Application;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Application.Admin;
using WakePark.Modules.Notifications.Application.Emails;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.UnitTests;

public sealed class AdminAlertTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly List<EmailMessage> _queued = [];
    private readonly IEmailRepository _emails = Substitute.For<IEmailRepository>();
    private readonly IEmailSender _sender = Substitute.For<IEmailSender>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public AdminAlertTests()
    {
        _emails.When(e => e.Add(Arg.Any<EmailMessage>())).Do(call => _queued.Add(call.Arg<EmailMessage>()));
        _clock.UtcNow.Returns(Now);
        _sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new IOException("SMTP nejde")));
    }

    [Fact]
    public async Task Kdyz_e_mail_zakaznikovi_definitivne_selze_prijde_upozorneni_na_info()
    {
        EmailMessage voucherEmail = Email("vouchers-issued", attemptsSoFar: EmailMessage.MaxAttempts - 1);

        await SendAsync(voucherEmail);

        voucherEmail.Status.ShouldBe(EmailStatus.Failed);
        EmailMessage alert = _queued.ShouldHaveSingleItem();
        alert.To.ShouldBe("info@wake-dzban.cz");
        alert.Subject.ShouldStartWith("[Wake Park e-shop] Nedoručený e-mail zákazníkovi");
        alert.TextBody.ShouldContain("jana@example.cz");
    }

    [Fact]
    public async Task Propadly_e_mail_take_upozorni()
    {
        EmailMessage stale = Email("order-placed", attemptsSoFar: 0);
        stale.CreatedAtUtc = Now.AddHours(-25);

        await SendAsync(stale);

        stale.Status.ShouldBe(EmailStatus.Expired);
        _queued.ShouldHaveSingleItem().Kind.ShouldBe(AdminAlerts.Kind);
    }

    [Fact]
    public async Task Upozorneni_na_upozorneni_se_neposila()
    {
        EmailMessage alert = Email(AdminAlerts.Kind, attemptsSoFar: EmailMessage.MaxAttempts - 1);

        await SendAsync(alert);

        alert.Status.ShouldBe(EmailStatus.Failed);
        _queued.ShouldBeEmpty("výpadek SMTP nesmí rozjet smyčku upozornění");
    }

    [Fact]
    public async Task Platba_k_rucnimu_vyrizeni_prijde_na_info_s_udaji_o_platbe()
    {
        PaymentNeedsAttentionIntegrationEvent payment = new(
            Guid.CreateVersion7(), Now, Guid.CreateVersion7(), new DateOnly(2026, 10, 22), 2500m, "CZK", "261000",
            "Nováková Jana", "1234567890/0800", null, "Nedoplatek: přišlo 2500.00, objednávka 2580.00.");

        await new PaymentNeedsAttentionConsumer(_emails, Substitute.For<INotificationsUnitOfWork>(), new NotificationsOptions())
            .HandleAsync(payment, TestContext.Current.CancellationToken);

        EmailMessage alert = _queued.ShouldHaveSingleItem();
        alert.To.ShouldBe("info@wake-dzban.cz");
        alert.Subject.ShouldContain("VS 261000");
        alert.TextBody.ShouldContain("Nedoplatek");
        alert.TextBody.ShouldContain("Nováková Jana");
    }

    private async Task SendAsync(EmailMessage message)
    {
        _emails.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([message]);

        await new SendPendingEmailsCommandHandler(_emails, _sender, Substitute.For<INotificationsUnitOfWork>(), _clock, new NotificationsOptions())
            .HandleAsync(new SendPendingEmailsCommand(), TestContext.Current.CancellationToken);
    }

    private static EmailMessage Email(string kind, int attemptsSoFar)
    {
        EmailMessage message = EmailMessage.Create(Guid.CreateVersion7(), kind, "jana@example.cz", "Vaše vouchery", "<p>x</p>", "x");
        message.CreatedAtUtc = Now.AddMinutes(-5);
        for (int i = 0; i < attemptsSoFar; i++)
        {
            message.MarkFailed("SMTP nejde", Now.AddMinutes(-10));
        }

        return message;
    }
}
