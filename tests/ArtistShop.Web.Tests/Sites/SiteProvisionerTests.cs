using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Sites;

[Collection(DatabaseCollection.Name)]
public sealed class SiteProvisionerTests : IAsyncDisposable
{
    private readonly DirectoryInfo _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
    private readonly ImageStorageSettings _storageSettings;
    private readonly SiteDatabases _siteDatabases;
    private readonly SiteProvisioner _provisioner;

    // an Identity user id; the platform database doesn't check it names an account
    private const string OwnerUserId = "test-owner";

    public SiteProvisionerTests(TestDatabaseFixture database)
    {
        _storageSettings = new ImageStorageSettings(_storageRoot.FullName);
        _siteDatabases = new SiteDatabases(
            database.PlatformConnectionString,
            TestDatabaseFixture.SiteDatabaseNamePrefix,
            new SiteDatabaseSettings { MaximumPoolSize = 2, ConnectionIdleLifetime = TimeSpan.FromSeconds(10) }
        );
        _provisioner = new SiteProvisioner(new SiteRepository(database.PlatformDataSource), _siteDatabases, _storageSettings);
    }

    public async ValueTask DisposeAsync()
    {
        await _siteDatabases.DisposeAsync();
        _storageRoot.Delete(recursive: true);
    }

    [Fact]
    public async Task ANewSiteHasItsOwnDatabaseAndFolders()
    {
        var siteId = await _provisioner.CreateAsync([NewHost()], OwnerUserId);

        // an empty, up-to-date site database: its functions are there to be called
        Assert.Empty(await new PostRepository(_siteDatabases.For(siteId)).GetAllAsync());

        var storage = ImageStorage.ForSite(_storageSettings, siteId);
        Assert.True(Directory.Exists(storage.Originals));
        Assert.True(Directory.Exists(storage.Variants));
    }

    // one site's post isn't in another's database
    [Fact]
    public async Task SitesKeepTheirDataApart()
    {
        var first = await _provisioner.CreateAsync([NewHost()], OwnerUserId);
        var second = await _provisioner.CreateAsync([NewHost()], OwnerUserId);

        await new PostRepository(_siteDatabases.For(first)).AddAsync(
            new PostTitle("Only on the first site"),
            PostSlug.FromTitle("Only on the first site"),
            new PostBody("""{"ops":[{"insert":"Hello\n"}]}"""),
            PostStatus.Published
        );

        Assert.Single(await new PostRepository(_siteDatabases.For(first)).GetAllAsync());
        Assert.Empty(await new PostRepository(_siteDatabases.For(second)).GetAllAsync());
    }

    [Fact]
    public async Task PreparingASiteAgainChangesNothing()
    {
        var siteId = await _provisioner.CreateAsync([NewHost()], OwnerUserId);

        _provisioner.Prepare(siteId);

        Assert.Empty(await new PostRepository(_siteDatabases.For(siteId)).GetAllAsync());
    }

    private static HostName NewHost() =>
        HostName.Read($"{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");
}
