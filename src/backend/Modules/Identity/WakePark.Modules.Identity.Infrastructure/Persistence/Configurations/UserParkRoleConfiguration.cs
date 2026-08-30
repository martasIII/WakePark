using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using WakePark.Modules.Identity.Domain.Authorization;
using WakePark.Modules.Identity.Domain.Users;

namespace WakePark.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserParkRoleConfiguration : IEntityTypeConfiguration<UserParkRole>
{
    public void Configure(EntityTypeBuilder<UserParkRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_park_roles");
        builder.HasKey(upr => upr.Id);

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(upr => upr.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(upr => upr.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Vyhledání přístupů uživatele je nejčastější dotaz celého modulu — běží při každém
        // autorizovaném požadavku.
        builder.HasIndex(upr => upr.UserAccountId);
        builder.HasIndex(upr => new { upr.UserAccountId, upr.WakeParkId, upr.RoleId }).IsUnique();

        builder.Ignore(upr => upr.DomainEvents);
    }
}
