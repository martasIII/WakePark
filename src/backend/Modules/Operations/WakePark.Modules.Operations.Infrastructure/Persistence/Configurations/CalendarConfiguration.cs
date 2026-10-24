using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Operations.Domain.Calendar;
using WakePark.Modules.Operations.Domain.Parks;

namespace WakePark.Modules.Operations.Infrastructure.Persistence.Configurations;

internal sealed class ParkSettingsConfiguration : IEntityTypeConfiguration<ParkSettings>
{
    public void Configure(EntityTypeBuilder<ParkSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("park_settings");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.WakeParkId).IsUnique();
        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class OperatingDayConfiguration : IEntityTypeConfiguration<OperatingDay>
{
    public void Configure(EntityTypeBuilder<OperatingDay> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("operating_days");
        builder.HasKey(d => d.Id);

        // Jeden záznam na den; podle areálu a data se i hledá (kalendář, rozvrh).
        builder.HasIndex(d => new { d.WakeParkId, d.Date }).IsUnique();
        builder.Ignore(d => d.DomainEvents);
    }
}

internal sealed class BlockingConfiguration : IEntityTypeConfiguration<Blocking>
{
    public void Configure(EntityTypeBuilder<Blocking> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("blockings");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(b => b.Note).HasMaxLength(Blocking.NoteMaxLength);

        builder.HasIndex(b => new { b.WakeParkId, b.StartUtc, b.EndUtc });
        builder.Ignore(b => b.DomainEvents);
    }
}
