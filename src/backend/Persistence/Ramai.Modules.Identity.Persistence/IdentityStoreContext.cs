using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ramai.Modules.Identity.Persistence;

public sealed class IdentityStoreContext(DbContextOptions<IdentityStoreContext> options)
    : IdentityUserContext<AccountUser, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        builder.Entity<AccountUser>(user =>
        {
            user.Property(x => x.Id).ValueGeneratedNever();
            user.ToTable("Users", table => table.HasCheckConstraint("CK_Users_Id", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid"));
            user.Property(x => x.Email).IsRequired().HasMaxLength(256);
            user.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(256);
            user.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("EmailIndex");
        });
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
    }
}
