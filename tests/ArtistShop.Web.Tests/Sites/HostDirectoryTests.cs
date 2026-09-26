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

    // with the site's main host, whichever host was asked for
    [Fact]
    public async Task FindsASiteByAnyOfItsHostsInAnyCase()
    {
        var label = Guid.NewGuid().ToString("n");
        var mainHost = Host($"{label}.test");
        var siteId = await _sites.AddNewAsync([mainHost, Host($"www.{label}.test")], OwnerUserId);
        var directory = NewDirectory();

        await directory.ReloadAsync();

        Assert.Equal(new CurrentHost.Site(siteId, mainHost), directory.Find($"{label}.test"));
        Assert.Equal(new CurrentHost.Site(siteId, mainHost), directory.Find($"WWW.{label.ToUpperInvariant()}.test"));
    }

    [Fact]
    public async Task FindsThePlatformByItsHostInAnyCase()
    {
        var platformHost = UniqueHost();
        var directory = new HostDirectory(_sites.GetHostsAsync, new PlatformSettings(platformHost, "PictureCord"));

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

        var siteId = await _sites.AddNewAsync([host], OwnerUserId);

        Assert.Null(directory.Find(host.Value));
        await directory.ReloadAsync();
        Assert.Equal(new CurrentHost.Site(siteId, host), directory.Find(host.Value));
    }

    // the platform would take every request for it, so the site could never be reached
    [Fact]
    public async Task RefusesASiteWithThePlatformsHost()
    {
        var host = UniqueHost();
        await _sites.AddNewAsync([host], OwnerUserId);
        var directory = new HostDirectory(_sites.GetHostsAsync, new PlatformSettings(host, "PictureCord"));

        await Assert.ThrowsAsync<InvalidOperationException>(directory.ReloadAsync);
    }

    // Two sign-ups at once: the earlier reload read the hosts before the second site was added, and
    // its read finishes last. It mustn't put back the list without that site
    [Fact]
    public async Task AnEarlierReloadDoesntUndoALaterOne()
    {
        var host = UniqueHost();
        var siteId = new SiteId(1);
        var earlierRead = new TaskCompletionSource<List<SiteHost>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var directory = new HostDirectory(
            () =>
                Interlocked.Increment(ref reads) is 1
                    ? earlierRead.Task
                    : Task.FromResult<List<SiteHost>>([new SiteHost(host, siteId, IsMain: true)]),
            new PlatformSettings(UniqueHost(), "PictureCord")
        );

        var earlier = directory.ReloadAsync();
        var later = directory.ReloadAsync();
        earlierRead.SetResult([]);
        await Task.WhenAll(earlier, later);

        Assert.Equal(new CurrentHost.Site(siteId, host), directory.Find(host.Value));
    }

    // a platform host of its own, since every test shares the platform test database
    private HostDirectory NewDirectory() => new(_sites.GetHostsAsync, new PlatformSettings(UniqueHost(), "PictureCord"));

    private static HostName UniqueHost() => Host($"{Guid.NewGuid():n}.test");

    private static HostName Host(string value) => HostName.Read(value) ?? throw new InvalidOperationException("Not a host.");
}
