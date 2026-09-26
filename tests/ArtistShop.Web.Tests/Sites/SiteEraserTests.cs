using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ArtistShop.Web.Tests.Sites;

// a whole site, schema and image folders, deleted and then left for its grace period
[Collection(DatabaseCollection.Name)]
public sealed class SiteEraserTests : IDisposable
{
    private const string OwnerUserId = "test-owner";

    private readonly TestDatabaseFixture _database;
    private readonly SiteRepository _sites;
    private readonly ImageStorageSettings _storageSettings;
    private readonly DirectoryInfo _storageRoot;
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly SiteEraser _eraser;

    public SiteEraserTests(TestDatabaseFixture database)
    {
        _database = database;
        _sites = new SiteRepository(database.PlatformDataSource);
        _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
        _storageSettings = new ImageStorageSettings(_storageRoot.FullName);
        _eraser = new SiteEraser(
            _sites,
            new SiteSchemas(database.PlatformConnectionString),
            _storageSettings,
            _time,
            NullLogger<SiteEraser>.Instance
        );
    }

    public void Dispose() => _storageRoot.Delete(recursive: true);

    [Fact]
    public async Task ErasesADeletedSiteOnlyOnceItsGracePeriodIsOver()
    {
        var siteId = await NewSiteAsync();
        await _sites.ScheduleDeletionAsync(siteId, OwnerUserId, SiteDeletion.EraseAt(_time.GetUtcNow()));

        _time.Advance(TimeSpan.FromDays(SiteDeletion.GraceDays) - TimeSpan.FromMinutes(1));
        await _eraser.EraseDueAsync();

        Assert.True(await SchemaExistsAsync(siteId));
        Assert.True(Directory.Exists(ImageStorage.ForSite(_storageSettings, siteId).Originals));
        Assert.Contains(siteId, await _sites.GetIdsAsync());

        _time.Advance(TimeSpan.FromMinutes(1));
        await _eraser.EraseDueAsync();

        Assert.False(await SchemaExistsAsync(siteId));
        Assert.False(Directory.Exists(ImageStorage.ForSite(_storageSettings, siteId).Originals));
        Assert.DoesNotContain(siteId, await _sites.GetIdsAsync());
    }

    [Fact]
    public async Task LeavesASiteThatWasKept()
    {
        var siteId = await NewSiteAsync();
        await _sites.ScheduleDeletionAsync(siteId, OwnerUserId, SiteDeletion.EraseAt(_time.GetUtcNow()));
        await _sites.KeepAsync(siteId, OwnerUserId);

        _time.Advance(TimeSpan.FromDays(SiteDeletion.GraceDays + 1));
        await _eraser.EraseDueAsync();

        Assert.True(await SchemaExistsAsync(siteId));
        Assert.Contains(siteId, await _sites.GetIdsAsync());
    }

    // a crash after the schema was dropped leaves the row, so the next run finishes the job
    [Fact]
    public async Task FinishesASiteErasedPartway()
    {
        var siteId = await NewSiteAsync();
        await _sites.ScheduleDeletionAsync(siteId, OwnerUserId, _time.GetUtcNow());
        await new SiteSchemas(_database.PlatformConnectionString).DropAsync(siteId);

        await _eraser.EraseDueAsync();

        Assert.DoesNotContain(siteId, await _sites.GetIdsAsync());
        Assert.False(Directory.Exists(ImageStorage.ForSite(_storageSettings, siteId).Originals));
    }

    // as sign-up makes one: its schema, its image folders and its row
    private async Task<SiteId> NewSiteAsync()
    {
        var host = HostName.Read($"erasing-{Guid.NewGuid():n}.test") ?? throw new InvalidOperationException("Not a host.");
        return await new SiteProvisioner(_sites, new SiteSchemas(_database.PlatformConnectionString), _storageSettings)
            .CreateAsync([host], OwnerUserId);
    }

    private async Task<bool> SchemaExistsAsync(SiteId siteId)
    {
        await using var connection = _database.PlatformDataSource.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = @Name)",
            new { Name = SiteSchema.Name(siteId) }
        );
    }
}
