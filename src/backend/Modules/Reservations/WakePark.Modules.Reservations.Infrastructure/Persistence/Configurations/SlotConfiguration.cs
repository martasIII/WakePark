using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Reservations.Domain.Slots;

namespace WakePark.Modules.Reservations.Infrastructure.Persistence.Configurations;

internal sealed class SlotConfiguration : IEntityTypeConfiguration<Slot>
{
    public void Configure(EntityTypeBuilder<Slot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("slots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        // Dvě rezervace na stejný čas nesmí založit dva sloty. Unikátní index je poslední
        // pojistka i pro případ, že by dva požadavky dorazily současně.
        builder.HasIndex(s => new { s.CableId, s.StartUtc }).IsUnique();

        // xmin je systémový sloupec Postgresu, který se mění při každém zápisu. Jako concurrency
        // token dá optimistický zámek zadarmo, bez vlastního sloupce s verzí.
        builder.Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.Ignore(s => s.DomainEvents);
        builder.Ignore(s => s.EndUtc);
        builder.Ignore(s => s.FreeCapacity);
    }
}
