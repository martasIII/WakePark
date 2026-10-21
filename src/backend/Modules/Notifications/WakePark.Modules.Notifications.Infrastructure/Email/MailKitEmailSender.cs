using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

using QRCoder;

using WakePark.Modules.Notifications.Application;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.Infrastructure.Email;

/// <summary>Odeslání přes SMTP (Wedos: STARTTLS na 587; při vývoji Mailpit bez šifrování).</summary>
internal sealed class MailKitEmailSender(NotificationsOptions options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using MimeMessage mime = new();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        mime.ReplyTo.Add(MailboxAddress.Parse(options.ReplyTo));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        BodyBuilder body = new() { HtmlBody = message.HtmlBody, TextBody = message.TextBody };
        foreach (EmailAttachment attachment in message.Attachments)
        {
            MimeEntity part = attachment.ContentId is null
                ? body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType))
                : body.LinkedResources.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));

            if (attachment.ContentId is not null)
            {
                part.ContentId = attachment.ContentId;
            }
        }

        mime.Body = body.ToMessageBody();

        SmtpOptions smtp = options.Smtp;
        using SmtpClient client = new();
        await client.ConnectAsync(
                smtp.Host,
                smtp.Port,
                smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class QrCodeImage : IQrCodeImage
{
    public byte[] Png(string content)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using PngByteQRCode qr = new(data);
        return qr.GetGraphic(8);
    }
}
