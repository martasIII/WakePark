using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.BuildingBlocks.Infrastructure.Persistence;
using WakePark.Modules.Notifications.Application.Abstractions;
using WakePark.Modules.Notifications.Domain.Emails;

namespace WakePark.Modules.Notifications.Infrastructure.Persistence;

/// <summary>Databázový kontext modulu Notifications — fronta e-mailů.</summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options), INotificationsUnitOfWork
{
    public const string Schema = "notifications";

    public DbSet<EmailMessage> Emails => Set<EmailMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        modelBuilder.ApplyWakeParkConventions();

        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("emails");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Kind).HasMaxLength(50).IsRequired();
        builder.Property(e => e.To).HasMaxLength(EmailMessage.AddressMaxLength).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(EmailMessage.SubjectMaxLength).IsRequired();
        builder.Property(e => e.HtmlBody).IsRequired();
        builder.Property(e => e.TextBody).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.LastError).HasMaxLength(2000);

        builder.OwnsMany(e => e.Attachments, attachment =>
        {
            attachment.ToTable("email_attachments");
            attachment.WithOwner().HasForeignKey("EmailId");
            attachment.HasKey(a => a.Id);
            attachment.Property(a => a.Id).ValueGeneratedNever();
            attachment.Property(a => a.FileName).HasMaxLength(200).IsRequired();
            attachment.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
            attachment.Property(a => a.ContentId).HasMaxLength(100);
        });

        builder.Navigation(e => e.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Inbox: stejná událost vyrobí e-mail daného druhu nejvýš jednou — i kdyby dva
        // dispatchery doručily totéž současně.
        builder.HasIndex(e => new { e.SourceEventId, e.Kind }).IsUnique();
        builder.HasIndex(e => new { e.Status, e.NextAttemptAtUtc });

        builder.Ignore(e => e.DomainEvents);
    }
}
