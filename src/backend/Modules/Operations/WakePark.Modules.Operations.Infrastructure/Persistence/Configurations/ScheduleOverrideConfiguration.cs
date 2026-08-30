using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Operations.Domain.Scheduling;

namespace WakePark.Modules.Operations.Infrastructure.Persistence.Configurations;

internal sealed class ScheduleOverrideConfiguration : IEntityTypeConfiguration<ScheduleOverride>
{
    public void Configure(EntityTypeBuilder<ScheduleOverride> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("schedule_overrides");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.CustomStartTime).HasColumnType("time");
        builder.Property(e => e.CustomEndTime).HasColumnType("time");

        // Na jeden vlek a den může být jen jedna výjimka — jinak by nebylo jasné, která platí.
        builder.HasIndex(e => new { e.CableId, e.Date }).IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}
