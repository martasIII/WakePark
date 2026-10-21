using WakePark.BuildingBlocks.Application.Abstractions;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.Application.Abstractions;

public interface IEmailRepository
{
    /// <summary>Už e-mail tohoto druhu z této události vznikl? (Opakované doručení z outboxu.)</summary>
    Task<bool> ExistsAsync(Guid sourceEventId, string kind, CancellationToken cancellationToken = default);

    /// <summary>E-maily čekající na odeslání, jejichž čas na další pokus už nastal.</summary>
    Task<IReadOnlyList<EmailMessage>> GetDueAsync(DateTimeOffset nowUtc, int max, CancellationToken cancellationToken = default);

    void Add(EmailMessage message);
}

/// <summary>Odeslání jednoho e-mailu (SMTP).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>QR kód jako PNG — do e-mailu jako obrázek v těle.</summary>
public interface IQrCodeImage
{
    byte[] Png(string content);
}

/// <summary>
/// PDF dokumenty do příloh. Za rozhraním kvůli licenci knihovny (ADR 0006) — výměna
/// knihovny znamená přepsat jednu třídu v infrastruktuře.
/// </summary>
public interface IDocumentRenderer
{
    byte[] VoucherPdf(Vouchers.Contracts.VoucherDto voucher);

    byte[] InvoicePdf(Billing.Contracts.InvoiceDto invoice);
}

/// <inheritdoc cref="IUnitOfWork" />
public interface INotificationsUnitOfWork : IUnitOfWork;
