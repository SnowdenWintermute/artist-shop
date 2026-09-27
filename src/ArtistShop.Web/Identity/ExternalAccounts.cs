namespace ArtistShop.Web.Identity;

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;

// what ExternalAccounts.AddLoginAsync did
public abstract record ExternalAccountResult
{
    // only the ones below
    private ExternalAccountResult() { }

    // the login is on an account now, so signing in with it works
    public sealed record Added : ExternalAccountResult;

    // the provider vouches for no email address, so there's no account to put the login on
    public sealed record NoVerifiedEmail : ExternalAccountResult;
}

// Puts a provider's login, such as Google's, on an account the first time someone signs in with it:
// the account with the email address the provider has verified, or a new one with that address. The
// provider has proved the address, as the link Register emails does, so linking needs no more than
// that; an account is tied to the login's id at the provider, not the address, from then on
public sealed class ExternalAccounts(UserManager<ApplicationUser> userManager)
{
    public const string EmailVerifiedClaimType = "email_verified";

    // for Google's claim action: "true" or "false" from its user information, which spells it
    // email_verified or, in its older version, verified_email
    public static string? ReadGoogleEmailVerified(JsonElement user) =>
        user.TryGetProperty("email_verified", out var verified) || user.TryGetProperty("verified_email", out verified)
            ? (verified.ValueKind is JsonValueKind.True ? "true" : "false")
            : null;

    // only for a login no account has yet, which ExternalLoginSignInAsync has just found
    public async Task<ExternalAccountResult> AddLoginAsync(ExternalLoginInfo login)
    {
        var email = login.Principal.FindFirstValue(ClaimTypes.Email);

        if (email is null || login.Principal.FindFirstValue(EmailVerifiedClaimType) != "true")
        {
            return new ExternalAccountResult.NoVerifiedEmail();
        }

        var account = await userManager.FindByEmailAsync(email);

        if (account is null)
        {
            // no password: the Password page's emailed link gives it one, if it's ever wanted
            account = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            SeededAccounts.ThrowIfFailed(await userManager.CreateAsync(account), $"Making an account for {login.LoginProvider}");
        }
        else if (!account.EmailConfirmed)
        {
            // Made before accounts waited for their email to be confirmed, so whoever chose its
            // password never proved the address; that could have been anyone. The password goes
            if (await userManager.HasPasswordAsync(account))
            {
                SeededAccounts.ThrowIfFailed(await userManager.RemovePasswordAsync(account), "Removing an unproved password");
            }

            account.EmailConfirmed = true;
            SeededAccounts.ThrowIfFailed(await userManager.UpdateAsync(account), "Confirming the email");
        }

        SeededAccounts.ThrowIfFailed(await userManager.AddLoginAsync(account, login), $"Adding the {login.LoginProvider} login");

        return new ExternalAccountResult.Added();
    }
}
