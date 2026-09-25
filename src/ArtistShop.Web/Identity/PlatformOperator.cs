using Microsoft.AspNetCore.Identity;

namespace ArtistShop.Web.Identity;

public static class PlatformPolicies
{
    // the platform operator: makes sign-up codes. No rights on any site's admin
    public const string Operator = "PlatformOperator";
}

// The one account that runs the platform, which Platform:OperatorEmail names. An Identity role,
// since unlike a site's admins it's the same on every host
public static class PlatformOperator
{
    public const string RoleName = "PlatformOperator";

    // Run at every startup, so the setting is the only say in who the operator is: the account it
    // names gets the role (made with Platform:OperatorPassword if it doesn't exist), and any other
    // account that has it loses it
    public static async Task SyncAsync(IServiceProvider services)
    {
        var email = services.GetRequiredService<IConfiguration>()["Platform:OperatorEmail"];

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Platform:OperatorEmail names no account to run the platform.");
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(RoleName))
        {
            SeededAccounts.ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole(RoleName)), "Creating the operator role");
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var operatorAccount = await SeededAccounts.FindOrCreateAsync(services, email, "Platform:OperatorPassword");

        foreach (var user in await userManager.GetUsersInRoleAsync(RoleName))
        {
            if (user.Id != operatorAccount.Id)
            {
                SeededAccounts.ThrowIfFailed(
                    await userManager.RemoveFromRoleAsync(user, RoleName),
                    $"Removing {user.Email} from the operator role"
                );
            }
        }

        if (!await userManager.IsInRoleAsync(operatorAccount, RoleName))
        {
            SeededAccounts.ThrowIfFailed(
                await userManager.AddToRoleAsync(operatorAccount, RoleName),
                "Making the operator account the operator"
            );
        }
    }
}
