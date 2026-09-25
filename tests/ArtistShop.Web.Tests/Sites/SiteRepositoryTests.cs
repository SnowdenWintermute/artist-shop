using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Sites;

[Collection(DatabaseCollection.Name)]
public sealed class SiteRepositoryTests(TestDatabaseFixture database)
{
    private readonly SiteRepository _sites = new(database.PlatformDataSource);

    // an Identity user id; the platform database doesn't check it names an account
    private const string OwnerUserId = "test-owner";

    // unique to one test, since every test shares the platform test database
    private static HostName NewHost(string label) =>
        HostName.Read($"{label}-{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");

    [Fact]
    public async Task KeepsASitesHostsWithTheFirstAsMain()
    {
        var main = NewHost("main");
        var other = NewHost("other");

        var siteId = await _sites.AddAsync([main, other], OwnerUserId);

        var hosts = (await _sites.GetHostsAsync()).Where(host => host.SiteId == siteId).OrderBy(host => host.Host.Value);
        Assert.Equivalent(new[] { new SiteHost(main, siteId, true), new SiteHost(other, siteId, false) }, hosts);
        Assert.Contains(siteId, await _sites.GetIdsAsync());
    }

    [Fact]
    public async Task RefusesAHostAnotherSiteHasAndAddsNoSite()
    {
        var taken = NewHost("taken");
        await _sites.AddAsync([taken], OwnerUserId);
        var sitesBefore = await _sites.GetIdsAsync();

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => _sites.AddAsync([NewHost("new"), taken], OwnerUserId));

        Assert.Equal(sitesBefore.Count, (await _sites.GetIdsAsync()).Count);
    }

    [Fact]
    public async Task RefusesAHostListedTwiceAndAddsNoSite()
    {
        var host = NewHost("twice");
        var sitesBefore = await _sites.GetIdsAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _sites.AddAsync([host, host], OwnerUserId));

        Assert.Equal(sitesBefore.Count, (await _sites.GetIdsAsync()).Count);
    }

    [Fact]
    public async Task TheSitesOwnerIsItsOnlyMember()
    {
        var siteId = await _sites.AddAsync([NewHost("owned")], OwnerUserId);

        Assert.Equal(SiteRole.Owner, await _sites.GetMemberRoleAsync(siteId, OwnerUserId));
        Assert.Null(await _sites.GetMemberRoleAsync(siteId, "someone-else"));
    }

    // membership is of one site, not every site the account is on
    [Fact]
    public async Task OwningOneSiteIsNoRoleOnAnother()
    {
        await _sites.AddAsync([NewHost("mine")], "first-owner");
        var other = await _sites.AddAsync([NewHost("theirs")], "second-owner");

        Assert.Null(await _sites.GetMemberRoleAsync(other, "first-owner"));
    }
}
