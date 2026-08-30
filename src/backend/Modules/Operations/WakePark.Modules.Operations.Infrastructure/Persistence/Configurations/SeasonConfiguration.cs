using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Operations.Domain.Seasons;

namespace WakePark.Modules.Operations.Infrastructure.Persistence.Configurations;

internal sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("seasons");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();

        // Vyhledání sezóny k datu je nejčastější dotaz celého modulu.
        builder.HasIndex(s => new { s.WakeParkId, s.StartDate, s.EndDate });
        builder.HasIndex(s => s.CableId);

        builder.Ignore(s => s.DomainEvents);
    }
}
