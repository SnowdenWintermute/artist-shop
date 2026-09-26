using System.Security.Claims;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.AspNetCore.Authorization;

namespace ArtistShop.Web.Tests.Sites;

[Collection(DatabaseCollection.Name)]
public sealed class SiteRoleHandlerTests(TestDatabaseFixture database)
{
    private readonly SiteRepository _sites = new(database.PlatformDataSource);
    private readonly SiteInviteRepository _invites = new(database.PlatformDataSource);

    // as Program.cs makes the two policies
    private static readonly SiteRoleRequirement Admin = new([SiteRole.Owner, SiteRole.Admin]);
    private static readonly SiteRoleRequirement Owner = new([SiteRole.Owner]);

    [Fact]
    public async Task TheSitesOwnerMayAdministerAndOwnIt()
    {
        var siteId = await _sites.AddNewAsync([NewHost()], "owner");

        Assert.True(await IsAllowedAsync(Admin, siteId, SignedIn("owner")));
        Assert.True(await IsAllowedAsync(Owner, siteId, SignedIn("owner")));
    }

    [Fact]
    public async Task AnAdminMayAdministerButNotOwnIt()
    {
        var siteId = await _sites.AddNewAsync([NewHost()], "owner");
        var adminUserId = await AddAdminAsync(siteId);

        Assert.True(await IsAllowedAsync(Admin, siteId, SignedIn(adminUserId)));
        Assert.False(await IsAllowedAsync(Owner, siteId, SignedIn(adminUserId)));
    }

    [Fact]
    public async Task AnAccountThatIsntAMemberMayNot()
    {
        var siteId = await _sites.AddNewAsync([NewHost()], "owner");

        Assert.False(await IsAllowedAsync(Admin, siteId, SignedIn("stranger")));
    }

    // the same account on the site next door
    [Fact]
    public async Task AnotherSitesOwnerMayNot()
    {
        await _sites.AddNewAsync([NewHost()], "other-owner");
        var siteId = await _sites.AddNewAsync([NewHost()], "owner");

        Assert.False(await IsAllowedAsync(Admin, siteId, SignedIn("other-owner")));
    }

    [Fact]
    public async Task SomeoneNotSignedInMayNot()
    {
        var siteId = await _sites.AddNewAsync([NewHost()], "owner");

        Assert.False(await IsAllowedAsync(Admin, siteId, new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public async Task NobodyAdministersThePlatformAsASite()
    {
        await _sites.AddNewAsync([NewHost()], "owner");

        Assert.False(await IsAllowedAsync(Admin, new CurrentHost.Platform(), SignedIn("owner")));
    }

    // the handler reads only the site's id, so any main host will do
    private Task<bool> IsAllowedAsync(SiteRoleRequirement requirement, SiteId siteId, ClaimsPrincipal user) =>
        IsAllowedAsync(requirement, new CurrentHost.Site(siteId, NewHost()), user);

    private async Task<bool> IsAllowedAsync(SiteRoleRequirement requirement, CurrentHost host, ClaimsPrincipal user)
    {
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new SiteRoleHandler(host, _sites).HandleAsync(context);

        return context.HasSucceeded;
    }

    // an admin as an accepted invitation makes one; their user id
    private async Task<string> AddAdminAsync(SiteId siteId)
    {
        var userId = $"admin-{Guid.NewGuid():n}";
        var email = EmailAddress.Read($"{userId}@example.com") ?? throw new InvalidOperationException("Not an email address.");
        await _invites.AddAsync(siteId, email, DateTimeOffset.UtcNow.AddDays(1));
        await _invites.AcceptAsync(siteId, email, userId);
        return userId;
    }

    // as Identity's sign-in cookie carries the account's id
    private static ClaimsPrincipal SignedIn(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "Test"));

    private static HostName NewHost() =>
        HostName.Read($"{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");
}
