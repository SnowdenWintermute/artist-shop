namespace ArtistShop.Web.Identity;

using System.Security.Claims;
using ArtistShop.Web.Database.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

// Signing in while another account is signed in ends that sign-in first. The cookie handler renews
// the sign-in the request came with rather than storing a new one (DatabaseTicketStore.RenewAsync),
// which would leave the browser under the other account's row. Signing out first makes it store a new
// row under a new key, so a copy of the old cookie never becomes this account's sign-in.
// Every way of signing in ends here, so this also gives the account its platform profile if it has none
public sealed class ApplicationSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    AccountProfileRepository accountProfiles
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

        await accountProfiles.AddAsync(user.Id);
        await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
    }
}
