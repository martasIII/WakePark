using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Operations.Domain.Cables;

namespace WakePark.Modules.Operations.Infrastructure.Persistence.Configurations;

internal sealed class CableConfiguration : IEntityTypeConfiguration<Cable>
{
    public void Configure(EntityTypeBuilder<Cable> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("cables");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);

        builder.HasIndex(c => c.WakeParkId);

        builder.Ignore(c => c.DomainEvents);
    }
}
