using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.BuildingBlocks.Domain;
using WakePark.BuildingBlocks.Domain.Abstractions;

namespace WakePark.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Integrační událost čekající na doručení. Zapisuje se **ve stejné transakci** jako změna,
/// která ji vyvolala — buď se uloží obojí, nebo nic. Pád procesu mezi uložením objednávky
/// a odesláním e-mailu tak e-mail neztratí; dispatcher ho doručí po restartu.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxAttempts = 10;

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Typ události ve tvaru „FullName, Assembly“ — podle něj se deserializuje.</summary>
    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage From(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        Type type = integrationEvent.GetType();
        return new OutboxMessage
        {
            // Id zprávy = Id události. Příjemce podle něj pozná opakované doručení.
            Id = integrationEvent.EventId,
            Type = $"{type.FullName}, {type.Assembly.GetName().Name}",
            Payload = JsonSerializer.Serialize(integrationEvent, type),
            OccurredAtUtc = integrationEvent.OccurredAtUtc,
        };
    }

    public IIntegrationEvent Deserialize() =>
        (IIntegrationEvent)(JsonSerializer.Deserialize(
                Payload,
                System.Type.GetType(Type, throwOnError: true)!)
            ?? throw new JsonException($"Prázdná událost v outboxu ({Id})."));

    public void MarkProcessed(DateTimeOffset nowUtc)
    {
        ProcessedAtUtc = nowUtc;
        LastError = null;
    }

    /// <summary>Neúspěch — další pokus s rostoucím odstupem (30 s, 1 min, 2 min … nejvýš 1 h).</summary>
    public void MarkFailed(string error, DateTimeOffset nowUtc)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        NextAttemptAtUtc = nowUtc + TimeSpan.FromSeconds(Math.Min(3600, 30 * Math.Pow(2, Attempts - 1)));
    }
}

/// <summary>Zápis do outboxu a jeho mapování v databázi — sdílené všemi moduly.</summary>
public static class Outbox
{
    /// <summary>
    /// Doménové události z rozpracovaných agregátů převede na integrační a přidá do outboxu.
    /// Volá se z <c>SaveChangesAsync</c> kontextu modulu, těsně před uložením.
    /// </summary>
    /// <param name="context">Kontext modulu, který se chystá ukládat.</param>
    /// <param name="map">Které doménové události modul zveřejní a jak; <c>null</c> = zůstane uvnitř.</param>
    public static void Collect(DbContext context, Func<IDomainEvent, IIntegrationEvent?> map)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(map);

        List<EntityEntry<AggregateRoot>> aggregates =
            [.. context.ChangeTracker.Entries<AggregateRoot>().Where(e => e.Entity.DomainEvents.Count > 0)];

        foreach (EntityEntry<AggregateRoot> entry in aggregates)
        {
            foreach (IDomainEvent domainEvent in entry.Entity.DomainEvents)
            {
                if (map(domainEvent) is { } integrationEvent)
                {
                    context.Set<OutboxMessage>().Add(OutboxMessage.From(integrationEvent));
                }
            }

            entry.Entity.ClearDomainEvents();
        }
    }

    /// <summary>Tabulka <c>outbox_messages</c> ve schématu modulu.</summary>
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();
            builder.Property(m => m.Type).HasMaxLength(500).IsRequired();
            builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            builder.Property(m => m.LastError).HasMaxLength(2000);

            // Dispatcher hledá nedoručené zprávy — částečný index drží jen ty.
            builder.HasIndex(m => m.OccurredAtUtc)
                .HasFilter("processed_at_utc IS NULL")
                .HasDatabaseName("ix_outbox_messages_pending");
        });

        return modelBuilder;
    }
}
