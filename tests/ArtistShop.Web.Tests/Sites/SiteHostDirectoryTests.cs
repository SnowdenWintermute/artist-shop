using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Sites;

[Collection(DatabaseCollection.Name)]
public sealed class SiteHostDirectoryTests(TestDatabaseFixture database)
{
    private readonly SiteRepository _sites = new(database.PlatformDataSource);

    // an Identity user id; the platform database doesn't check it names an account
    private const string OwnerUserId = "test-owner";

    [Fact]
    public async Task FindsASiteByAnyOfItsHostsInAnyCase()
    {
        var label = Guid.NewGuid().ToString("n");
        var siteId = await _sites.AddAsync([Host($"{label}.test"), Host($"www.{label}.test")], OwnerUserId);
        var directory = new SiteHostDirectory(_sites);

        await directory.ReloadAsync();

        Assert.Equal(siteId, directory.FindSite($"{label}.test"));
        Assert.Equal(siteId, directory.FindSite($"WWW.{label.ToUpperInvariant()}.test"));
    }

    [Fact]
    public async Task FindsNoSiteForAnUnknownHostOrAnAddress()
    {
        var directory = new SiteHostDirectory(_sites);

        await directory.ReloadAsync();

        Assert.Null(directory.FindSite($"{Guid.NewGuid():n}.test"));
        Assert.Null(directory.FindSite("127.0.0.1"));
    }

    [Fact]
    public async Task SeesASiteAddedSinceOnlyAfterReloading()
    {
        var directory = new SiteHostDirectory(_sites);
        await directory.ReloadAsync();
        var host = Host($"{Guid.NewGuid():n}.test");

        var siteId = await _sites.AddAsync([host], OwnerUserId);

        Assert.Null(directory.FindSite(host.Value));
        await directory.ReloadAsync();
        Assert.Equal(siteId, directory.FindSite(host.Value));
    }

    private static HostName Host(string value) => HostName.Read(value) ?? throw new InvalidOperationException("Not a host.");
}
