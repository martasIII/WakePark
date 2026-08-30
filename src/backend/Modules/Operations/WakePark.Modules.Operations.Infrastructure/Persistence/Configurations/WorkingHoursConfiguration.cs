using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Operations.Domain.Scheduling;

namespace WakePark.Modules.Operations.Infrastructure.Persistence.Configurations;

internal sealed class WorkingHoursConfiguration : IEntityTypeConfiguration<WorkingHours>
{
    public void Configure(EntityTypeBuilder<WorkingHours> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("working_hours");
        builder.HasKey(w => w.Id);

        // Lokální denní doba bez zóny — typ `time`, ne `timestamptz`.
        builder.Property(w => w.StartTime).HasColumnType("time");
        builder.Property(w => w.EndTime).HasColumnType("time");

        // Den v týdnu ukládáme jako číslo, ne jako text: je to fixní číselník BCL.
        builder.Property(w => w.DayOfWeek).HasConversion<int>();

        builder.HasIndex(w => new { w.CableId, w.SeasonId, w.DayOfWeek });

        builder.Ignore(w => w.DomainEvents);
    }
}
