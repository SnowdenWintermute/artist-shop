namespace ArtistShop.Web.Sites;

using System.Security.Claims;
using ArtistShop.Web.Database.Repositories;
using Microsoft.AspNetCore.Authorization;

public static class SitePolicies
{
    // the current site's owner or one of its admins
    public const string Admin = "SiteAdmin";
}

public sealed class SiteAdminRequirement : IAuthorizationRequirement;

// Accounts are shared by every site, so being signed in says nothing about which sites a person may
// administer; their membership of the current site does. Asked on every check rather than kept, so
// a removed admin loses access at once
public sealed class SiteAdminHandler(CurrentSite currentSite, SiteRepository siteRepository)
    : AuthorizationHandler<SiteAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SiteAdminRequirement requirement
    )
    {
        // Identity keeps the account's id in this claim
        if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId)
        {
            return;
        }

        // either role administers the site's content
        if (await siteRepository.GetMemberRoleAsync(currentSite.Id, userId) is not null)
        {
            context.Succeed(requirement);
        }
    }
}
