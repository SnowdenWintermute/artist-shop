namespace ArtistShop.Web.Identity;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

// Signing in while another account is signed in ends that sign-in first. The cookie handler renews
// the sign-in the request came with rather than storing a new one (DatabaseTicketStore.RenewAsync),
// which would leave the browser under the other account's row. Signing out first makes it store a new
// row under a new key, so a copy of the old cookie never becomes this account's sign-in
public sealed class ApplicationSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation
) : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task SignInWithClaimsAsync(
        ApplicationUser user,
        AuthenticationProperties? authenticationProperties,
        IEnumerable<Claim> additionalClaims
    )
    {
        if (UserManager.GetUserId(Context.User) is { } signedInId && signedInId != user.Id)
        {
            await Context.SignOutAsync(IdentityConstants.ApplicationScheme);
        }

        await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
    }
}
