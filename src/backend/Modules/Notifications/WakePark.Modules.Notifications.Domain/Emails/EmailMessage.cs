using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.Modules.Notifications.Domain.Emails;

/// <summary>
/// E-mail ve frontě k odeslání. Vzniká z integrační události a odesílá se zvlášť — když SMTP
/// zrovna nejde, e-mail počká a zkusí se znovu, událost se kvůli tomu znovu doručovat nemusí.
/// </summary>
/// <remarks>
/// <see cref="SourceEventId"/> + <see cref="Kind"/> jsou unikátní: outbox doručuje „aspoň
/// jednou“, ale zákazník stejný e-mail dostane nejvýš jednou.
/// </remarks>
public sealed class EmailMessage : AggregateRoot, IAuditable
{
    public const int MaxAttempts = 8;
    public const int AddressMaxLength = 254;
    public const int SubjectMaxLength = 200;

    private readonly List<EmailAttachment> _attachments = [];

    private EmailMessage()
    {
    }

    private EmailMessage(Guid id)
        : base(id)
    {
    }

    public Guid SourceEventId { get; private set; }

    /// <summary>Druh e-mailu, např. <c>order-placed</c>.</summary>
    public string Kind { get; private set; } = string.Empty;

    public string To { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string HtmlBody { get; private set; } = string.Empty;

    public string TextBody { get; private set; } = string.Empty;

    public IReadOnlyCollection<EmailAttachment> Attachments => _attachments.AsReadOnly();

    public EmailStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static EmailMessage Create(
        Guid sourceEventId,
        string kind,
        string to,
        string subject,
        string htmlBody,
        string textBody,
        IEnumerable<EmailAttachment>? attachments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(textBody);

        if (sourceEventId == Guid.Empty)
        {
            throw new DomainException("email.source_required", "E-mail musí mít událost, ze které vznikl.");
        }

        if (string.IsNullOrWhiteSpace(to) || to.Length > AddressMaxLength || !to.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainException("email.invalid_recipient", "Neplatný příjemce e-mailu.");
        }

        if (string.IsNullOrWhiteSpace(subject) || subject.Length > SubjectMaxLength)
        {
            throw new DomainException("email.invalid_subject", "Neplatný předmět e-mailu.");
        }

        EmailMessage message = new(NewId())
        {
            SourceEventId = sourceEventId,
            Kind = kind,
            To = to.Trim(),
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            Status = EmailStatus.Pending,
        };

        message._attachments.AddRange(attachments ?? []);
        return message;
    }

    public void MarkSent(DateTimeOffset nowUtc)
    {
        Status = EmailStatus.Sent;
        SentAtUtc = nowUtc;
        LastError = null;
        NextAttemptAtUtc = null;
    }

    /// <summary>Neúspěch — další pokus s rostoucím odstupem (1, 2, 4 … min); po posledním pokusu konec.</summary>
    public void MarkFailed(string error, DateTimeOffset nowUtc)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;

        if (Attempts >= MaxAttempts)
        {
            Status = EmailStatus.Failed;
            NextAttemptAtUtc = null;
            return;
        }

        NextAttemptAtUtc = nowUtc + TimeSpan.FromMinutes(Math.Pow(2, Attempts - 1));
    }

    /// <summary>
    /// E-mail se neposlal včas (odesílání bylo vypnuté nebo SMTP dlouho nešlo). Potvrzení
    /// objednávky staré několik dní by zákazníka spíš zmátlo — raději se neodešle vůbec.
    /// </summary>
    public void Expire(DateTimeOffset nowUtc)
    {
        Status = EmailStatus.Expired;
        NextAttemptAtUtc = null;
        LastError = $"Neodesláno do {nowUtc:u}, zastaralé.";
    }
}

/// <summary>Příloha e-mailu. S <see cref="ContentId"/> je to obrázek vložený do těla (QR kód).</summary>
public sealed class EmailAttachment
{
    private EmailAttachment()
    {
    }

    public EmailAttachment(string fileName, string contentType, byte[] content, string? contentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(content);

        Id = Guid.CreateVersion7();
        FileName = fileName;
        ContentType = contentType;
        Content = content;
        ContentId = contentId;
    }

    public Guid Id { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public string? ContentId { get; private set; }

#pragma warning disable CA1819 // Obsah přílohy je pole bajtů z podstaty věci (bytea).
    public byte[] Content { get; private set; } = [];
#pragma warning restore CA1819
}

public enum EmailStatus
{
    Pending,
    Sent,
    Failed,
    Expired,
}
