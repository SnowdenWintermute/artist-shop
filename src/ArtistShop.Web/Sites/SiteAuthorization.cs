namespace ArtistShop.Web.Sites;

using System.Security.Claims;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using Microsoft.AspNetCore.Authorization;

public static class SitePolicies
{
    // the current site's owner or one of its admins
    public const string Admin = "SiteAdmin";

    // the current site's owner only
    public const string Owner = "SiteOwner";
}

// a member of the current site with one of these roles
public sealed record SiteRoleRequirement(IReadOnlyList<SiteRole> Roles) : IAuthorizationRequirement;

// Accounts are shared by every site, so being signed in says nothing about which sites a person may
// administer; their membership of the current site does. Asked on every check rather than kept, so
// a removed admin loses access at once. Takes CurrentHost rather than CurrentSite: Blazor makes every
// handler whenever a page asks for IAuthorizationService, on the platform's host too
public sealed class SiteRoleHandler(CurrentHost currentHost, SiteRepository siteRepository)
    : AuthorizationHandler<SiteRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SiteRoleRequirement requirement
    )
    {
        // the platform isn't a site, so nobody administers it as one. Identity keeps the account's id
        // in this claim
        if (currentHost is not CurrentHost.Site site || context.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId)
        {
            return;
        }

        if (await siteRepository.GetMemberRoleAsync(site.Id, userId) is { } role && requirement.Roles.Contains(role))
        {
            context.Succeed(requirement);
        }
    }
}
