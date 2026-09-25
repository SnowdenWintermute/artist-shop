using System.Security.Claims;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.AspNetCore.Authorization;

namespace ArtistShop.Web.Tests.Sites;

[Collection(DatabaseCollection.Name)]
public sealed class SiteAdminHandlerTests(TestDatabaseFixture database)
{
    private readonly SiteRepository _sites = new(database.PlatformDataSource);

    [Fact]
    public async Task TheSitesOwnerMayAdministerIt()
    {
        var siteId = await _sites.AddAsync([NewHost()], "owner");

        Assert.True(await IsSiteAdminAsync(siteId, SignedIn("owner")));
    }

    [Fact]
    public async Task AnAccountThatIsntAMemberMayNot()
    {
        var siteId = await _sites.AddAsync([NewHost()], "owner");

        Assert.False(await IsSiteAdminAsync(siteId, SignedIn("stranger")));
    }

    // the same account on the site next door
    [Fact]
    public async Task AnotherSitesOwnerMayNot()
    {
        await _sites.AddAsync([NewHost()], "other-owner");
        var siteId = await _sites.AddAsync([NewHost()], "owner");

        Assert.False(await IsSiteAdminAsync(siteId, SignedIn("other-owner")));
    }

    [Fact]
    public async Task SomeoneNotSignedInMayNot()
    {
        var siteId = await _sites.AddAsync([NewHost()], "owner");

        Assert.False(await IsSiteAdminAsync(siteId, new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private async Task<bool> IsSiteAdminAsync(SiteId siteId, ClaimsPrincipal user)
    {
        var requirement = new SiteAdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new SiteAdminHandler(new CurrentSite(siteId), _sites).HandleAsync(context);

        return context.HasSucceeded;
    }

    // as Identity's sign-in cookie carries the account's id
    private static ClaimsPrincipal SignedIn(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "Test"));

    private static HostName NewHost() =>
        HostName.Read($"{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");
}
