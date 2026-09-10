using Microsoft.AspNetCore.Identity;

namespace ArtistShop.Web.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
        {
            ThrowIfFailed(
                await roleManager.CreateAsync(new IdentityRole(RoleNames.Admin)),
                "creating Admin role"
            );
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync("mike@example.com");

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = "mike@example.com",
                Email = "mike@example.com",
            };
            var configuration = services.GetRequiredService<IConfiguration>();
            var devAdminPassword =
                configuration["DEV_ADMIN_PASSWORD"]
                ?? throw new InvalidOperationException("no dev password in env");

            ThrowIfFailed(
                await userManager.CreateAsync(user, devAdminPassword),
                "Creating the admin user"
            );
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;

            ThrowIfFailed(await userManager.UpdateAsync(user), "confirming the admin user's email");
        }

        var userIsAdmin = await userManager.IsInRoleAsync(user, RoleNames.Admin);

        if (!userIsAdmin)
        {
            ThrowIfFailed(
                await userManager.AddToRoleAsync(user, RoleNames.Admin),
                "adding user to Admin role"
            );
        }
    }

    private static void ThrowIfFailed(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));

        throw new InvalidOperationException($"{operation} failed. {errors}");
    }
}
