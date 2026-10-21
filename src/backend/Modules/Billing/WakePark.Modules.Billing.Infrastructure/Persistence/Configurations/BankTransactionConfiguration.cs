using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Billing.Domain.Payments;

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Configurations;

internal sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("bank_transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.ExternalId).HasMaxLength(BankTransaction.ExternalIdMaxLength).IsRequired();
        builder.Property(t => t.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(t => t.VariableSymbol).HasMaxLength(20);
        builder.Property(t => t.CounterpartyAccount).HasMaxLength(BankTransaction.TextMaxLength);
        builder.Property(t => t.CounterpartyName).HasMaxLength(BankTransaction.TextMaxLength);
        builder.Property(t => t.Message).HasMaxLength(BankTransaction.TextMaxLength);
        builder.Property(t => t.Note).HasMaxLength(BankTransaction.TextMaxLength);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        // Pojistka inboxu: stejný pohyb z banky se do tabulky nedostane dvakrát, ani kdyby
        // se aplikace spletla. Na tomhle indexu stojí „jedna platba = nejvýš jeden voucher“.
        builder.HasIndex(t => new { t.EnterpriseId, t.ExternalId }).IsUnique();
        builder.HasIndex(t => new { t.EnterpriseId, t.Status });
        builder.HasIndex(t => t.OrderId);

        builder.Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.Ignore(t => t.DomainEvents);
    }
}
