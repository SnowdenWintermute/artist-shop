using Microsoft.AspNetCore.Identity;

namespace ArtistShop.Web.Identity;

// accounts that configuration names, such as the platform operator's, made at startup
public static class SeededAccounts
{
    // The account with this email, made with the password at passwordKey if it doesn't exist yet,
    // with its email confirmed. The password is only read to make the account, so production can
    // drop it once the account exists
    public static async Task<ApplicationUser> FindOrCreateAsync(
        IServiceProvider services,
        string email,
        string passwordKey
    )
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email };
            var password =
                services.GetRequiredService<IConfiguration>()[passwordKey]
                ?? throw new InvalidOperationException($"{email} has no account yet, and {passwordKey} is not set");

            ThrowIfFailed(await userManager.CreateAsync(user, password), $"Creating the account {email}");
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;

            ThrowIfFailed(await userManager.UpdateAsync(user), $"confirming {email}");
        }

        return user;
    }

    public static void ThrowIfFailed(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));

        throw new InvalidOperationException($"{operation} failed. {errors}");
    }
}
