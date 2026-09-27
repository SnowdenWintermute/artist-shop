using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ArtistShop.Web.Identity;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<SiteSignInCode> SiteSignInCodes => Set<SiteSignInCode>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // no filter for users without an email: Postgres lets any number of NULLs past a UNIQUE
        builder.Entity<ApplicationUser>().HasIndex(user => user.NormalizedEmail).IsUnique();

        builder.Entity<UserSession>(session =>
        {
            session.Property(row => row.Id).HasMaxLength(64);
            // deleting an account ends its sign-ins
            session.HasOne<ApplicationUser>().WithMany().HasForeignKey(row => row.UserId).OnDelete(DeleteBehavior.Cascade);
            session.HasIndex(row => row.ExpiresAt);
            session.HasOne<UserSession>().WithMany().HasForeignKey(row => row.ParentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SiteSignInCode>(code =>
        {
            code.HasKey(row => row.CodeHash);
            code.Property(row => row.CodeHash).HasMaxLength(64);
            code.HasOne<ApplicationUser>().WithMany().HasForeignKey(row => row.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
