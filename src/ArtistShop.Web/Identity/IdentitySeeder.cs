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

        // Admin:Email names the account to make an admin, in every environment. With none set there
        // is nothing to seed. The password is only read to create the account, so production can
        // drop it after the first boot
        var configuration = services.GetRequiredService<IConfiguration>();
        var adminEmail = configuration["Admin:Email"];

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(adminEmail);

        if (user is null)
        {
            user = new ApplicationUser { UserName = adminEmail, Email = adminEmail };
            var adminPassword =
                configuration["Admin:Password"]
                ?? throw new InvalidOperationException(
                    $"Admin:Email is {adminEmail}, which has no account yet, and Admin:Password is not set"
                );

            ThrowIfFailed(
                await userManager.CreateAsync(user, adminPassword),
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
