using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Sites;

[Collection(DatabaseCollection.Name)]
public sealed class SiteRepositoryTests(TestDatabaseFixture database)
{
    private readonly SiteRepository _sites = new(database.PlatformDataSource);

    // unique to one test, since every test shares the platform test database
    private static HostName NewHost(string label) =>
        HostName.Read($"{label}-{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");

    [Fact]
    public async Task KeepsASitesHostsWithTheFirstAsMain()
    {
        var main = NewHost("main");
        var other = NewHost("other");

        var siteId = await _sites.AddAsync([main, other]);

        var hosts = (await _sites.GetHostsAsync()).Where(host => host.SiteId == siteId).OrderBy(host => host.Host.Value);
        Assert.Equivalent(new[] { new SiteHost(main, siteId, true), new SiteHost(other, siteId, false) }, hosts);
        Assert.Contains(siteId, await _sites.GetIdsAsync());
    }

    [Fact]
    public async Task RefusesAHostAnotherSiteHasAndAddsNoSite()
    {
        var taken = NewHost("taken");
        await _sites.AddAsync([taken]);
        var sitesBefore = await _sites.GetIdsAsync();

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => _sites.AddAsync([NewHost("new"), taken]));

        Assert.Equal(sitesBefore.Count, (await _sites.GetIdsAsync()).Count);
    }
}
