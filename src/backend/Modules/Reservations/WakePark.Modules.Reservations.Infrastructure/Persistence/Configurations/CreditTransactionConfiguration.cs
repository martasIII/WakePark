using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Reservations.Domain.Passes;

namespace WakePark.Modules.Reservations.Infrastructure.Persistence.Configurations;

internal sealed class CreditTransactionConfiguration : IEntityTypeConfiguration<CreditTransaction>
{
    public void Configure(EntityTypeBuilder<CreditTransaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("credit_transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.Note).HasMaxLength(500);

        builder.HasIndex(t => t.RidePassId);
        builder.HasIndex(t => t.ReservationId);
    }
}
