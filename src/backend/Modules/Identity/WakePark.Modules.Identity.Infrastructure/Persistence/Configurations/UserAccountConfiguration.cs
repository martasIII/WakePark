using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Identity.Domain.Users;

namespace WakePark.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_accounts");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);

        // E-mail je přihlašovací identifikátor, ukládá se normalizovaný na malá písmena.
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.Ignore(u => u.DomainEvents);
        builder.Ignore(u => u.CanSignIn);
    }
}
