using Microsoft.AspNetCore.Identity;

namespace ArtistShop.Web.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            ThrowIfFailed(
                await roleManager.CreateAsync(new IdentityRole("Admin")),
                "creating Admin role"
            );
        }

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        var user = await userManager.FindByNameAsync("Mike");

        if (user is null)
        {
            user = new IdentityUser { UserName = "Mike", Email = "mike@example.com" };
            var configuration = services.GetRequiredService<IConfiguration>();
            var devAdminPassword =
                configuration["DEV_ADMIN_PASSWORD"]
                ?? throw new InvalidOperationException("no dev password in env");

            ThrowIfFailed(
                await userManager.CreateAsync(user, devAdminPassword),
                "Creating the admin user"
            );
        }

        var userIsAdmin = await userManager.IsInRoleAsync(user, "Admin");

        if (!userIsAdmin)
        {
            ThrowIfFailed(
                await userManager.AddToRoleAsync(user, "Admin"),
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
