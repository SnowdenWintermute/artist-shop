using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Sites;

[Collection(DatabaseCollection.Name)]
public sealed class HostDirectoryTests(TestDatabaseFixture database)
{
    private readonly SiteRepository _sites = new(database.PlatformDataSource);

    // an Identity user id; the platform database doesn't check it names an account
    private const string OwnerUserId = "test-owner";

    [Fact]
    public async Task FindsASiteByAnyOfItsHostsInAnyCase()
    {
        var label = Guid.NewGuid().ToString("n");
        var siteId = await _sites.AddAsync([Host($"{label}.test"), Host($"www.{label}.test")], OwnerUserId);
        var directory = NewDirectory();

        await directory.ReloadAsync();

        Assert.Equal(new CurrentHost.Site(siteId), directory.Find($"{label}.test"));
        Assert.Equal(new CurrentHost.Site(siteId), directory.Find($"WWW.{label.ToUpperInvariant()}.test"));
    }

    [Fact]
    public async Task FindsThePlatformByItsHostInAnyCase()
    {
        var platformHost = UniqueHost();
        var directory = new HostDirectory(_sites, new PlatformSettings(platformHost));

        await directory.ReloadAsync();

        Assert.Equal(new CurrentHost.Platform(), directory.Find(platformHost.Value.ToUpperInvariant()));
    }

    [Fact]
    public async Task FindsNothingForAnUnknownHostOrAnAddress()
    {
        var directory = NewDirectory();

        await directory.ReloadAsync();

        Assert.Null(directory.Find(UniqueHost().Value));
        Assert.Null(directory.Find("127.0.0.1"));
    }

    [Fact]
    public async Task SeesASiteAddedSinceOnlyAfterReloading()
    {
        var directory = NewDirectory();
        await directory.ReloadAsync();
        var host = UniqueHost();

        var siteId = await _sites.AddAsync([host], OwnerUserId);

        Assert.Null(directory.Find(host.Value));
        await directory.ReloadAsync();
        Assert.Equal(new CurrentHost.Site(siteId), directory.Find(host.Value));
    }

    // the platform would take every request for it, so the site could never be reached
    [Fact]
    public async Task RefusesASiteWithThePlatformsHost()
    {
        var host = UniqueHost();
        await _sites.AddAsync([host], OwnerUserId);
        var directory = new HostDirectory(_sites, new PlatformSettings(host));

        await Assert.ThrowsAsync<InvalidOperationException>(directory.ReloadAsync);
    }

    // a platform host of its own, since every test shares the platform test database
    private HostDirectory NewDirectory() => new(_sites, new PlatformSettings(UniqueHost()));

    private static HostName UniqueHost() => Host($"{Guid.NewGuid():n}.test");

    private static HostName Host(string value) => HostName.Read(value) ?? throw new InvalidOperationException("Not a host.");
}
