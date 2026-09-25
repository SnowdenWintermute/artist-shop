using Microsoft.AspNetCore.Identity;

namespace ArtistShop.Web.Identity;

public static class FirstSiteOwner
{
    // The account FirstSite:OwnerEmail names, made with FirstSite:OwnerPassword if it doesn't exist
    // yet. Only asked for while there are no sites, so both settings can go once the first is made
    public static async Task<ApplicationUser> FindOrCreateAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var email = configuration["FirstSite:OwnerEmail"];

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "There are no sites yet, and FirstSite:OwnerEmail names no account to own the first one."
            );
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email };
            var password =
                configuration["FirstSite:OwnerPassword"]
                ?? throw new InvalidOperationException(
                    $"FirstSite:OwnerEmail is {email}, which has no account yet, and FirstSite:OwnerPassword is not set"
                );

            ThrowIfFailed(await userManager.CreateAsync(user, password), "Creating the first site's owner");
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;

            ThrowIfFailed(await userManager.UpdateAsync(user), "confirming the first site's owner's email");
        }

        return user;
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
