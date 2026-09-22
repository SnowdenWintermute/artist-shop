using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ArtistShop.Web.Identity;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // no filter for users without an email: Postgres lets any number of NULLs past a UNIQUE
        builder.Entity<ApplicationUser>().HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
