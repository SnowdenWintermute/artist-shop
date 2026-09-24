using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetVips;

namespace ArtistShop.Web.Tests.Images;

[Collection(DatabaseCollection.Name)]
public sealed class OrphanedImageSweeperTests : IDisposable
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    private readonly TestDatabaseFixture _database;
    private readonly DirectoryInfo _storageRoot;
    private readonly ImageStorageSettings _storageSettings;
    private readonly ImageStorage _imageStorage;
    private readonly ImageUploadStore _uploadStore;
    private readonly FakeTimeProvider _time;
    private readonly OrphanedImageSweeper _sweeper;

    // xUnit creates a NEW instance of this class for every test, so this constructor runs
    // before each one: every test gets its own empty storage directory and its own clock
    public OrphanedImageSweeperTests(TestDatabaseFixture database)
    {
        _database = database;

        _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
        _storageSettings = new ImageStorageSettings(_storageRoot.FullName);
        _imageStorage = ImageStorage.ForSite(_storageSettings, new SiteId(1));
        _uploadStore = UploadStoreFor(_imageStorage);
        _imageStorage.CreateFolders();

        var startTime = DateTimeOffset.UtcNow;
        _time = new FakeTimeProvider(startTime);

        _sweeper = new OrphanedImageSweeper(
            new OrphanedImageSweepSettings { GracePeriod = GracePeriod, Interval = TimeSpan.FromDays(1) },
            _time,
            NullLogger<OrphanedImageSweeper>.Instance
        );
    }

    // xUnit calls Dispose after each test
    public void Dispose() => _storageRoot.Delete(recursive: true);

    [Fact] // x-unit attribute meaning "this is a test, it take no parameters"
    public async Task DeletesUnreferencedUploadOlderThanGracePeriod()
    {
        var storageKey = await UploadImageAsync();

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.False(_imageStorage.OriginalExists(storageKey));
        Assert.False(Directory.Exists(_imageStorage.VariantDirectory(storageKey)));
    }

    [Fact]
    public async Task KeepsUnreferencedUploadInsideGracePeriod()
    {
        var storageKey = await UploadImageAsync();

        _time.Advance(GracePeriod - TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
        Assert.True(Directory.Exists(_imageStorage.VariantDirectory(storageKey)));
    }

    [Fact]
    public async Task KeepsReferencedUploadOlderThanGracePeriod()
    {
        var storageKey = await UploadImageAsync();
        await AddPaintingReferencing(storageKey);

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
        Assert.True(Directory.Exists(_imageStorage.VariantDirectory(storageKey)));
    }

    [Fact]
    public async Task KeepsUploadAPostNamesOlderThanGracePeriod()
    {
        var storageKey = await UploadImageAsync();
        await new PostRepository(_database.DataSource).AddAsync(
            new PostTitle($"Sweeper test post {storageKey}"),
            PostSlug.FromTitle($"Sweeper test post {storageKey}"),
            new PostBody(
                """{"ops":[{"insert":{"artshop-image":{"storageKey":"KEY","width":800,"height":600}}},{"insert":"\n"}]}"""
                    .Replace("KEY", storageKey)
            ),
            PostStatus.Draft
        );

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
        Assert.True(Directory.Exists(_imageStorage.VariantDirectory(storageKey)));
    }

    [Fact]
    public async Task LeavesFilesNotNamedLikeUploadsAlone()
    {
        var strayFilePath = Path.Combine(_imageStorage.Originals, "notes.txt");
        File.WriteAllText(strayFilePath, "not an upload");

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(File.Exists(strayFilePath));
    }

    // A second site shares the test database, since there is one database per test run. Its
    // orphan is never looked at, because a sweep only reads its own site's folder
    [Fact]
    public async Task LeavesAnotherSitesFilesAlone()
    {
        var otherSiteStorage = ImageStorage.ForSite(_storageSettings, new SiteId(2));
        otherSiteStorage.CreateFolders();
        var otherSiteKey = await UploadImageAsync(UploadStoreFor(otherSiteStorage));

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(otherSiteStorage.OriginalExists(otherSiteKey));
        Assert.True(Directory.Exists(otherSiteStorage.VariantDirectory(otherSiteKey)));
    }

    private Task SweepAsync() =>
        _sweeper.SweepAsync(
            _imageStorage,
            new ArtworkImageRepository(_database.DataSource),
            new PostRepository(_database.DataSource)
        );

    private static ImageUploadStore UploadStoreFor(ImageStorage imageStorage) =>
        new(
            imageStorage,
            new ImageProcessor(imageStorage),
            TestImageProcessing.CreateAmpleLimiter(),
            TestImageProcessing.Settings
        );

    private Task<string> UploadImageAsync() => UploadImageAsync(_uploadStore);

    private static async Task<string> UploadImageAsync(ImageUploadStore uploadStore)
    {
        // NetVips can create an image from nothing. Black, 800 by 600, 3 colour channels (red,
        // green, blue) like a real photo: wide enough to get variants
        using var image = Image.Black(800, 600, bands: 3);
        using var content = new MemoryStream(image.WriteToBuffer(".jpg"));

        // TestContext.Current.CancellationToken is cancelled if the test run is stopped or times
        // out; xUnit's analyzer asks for it wherever a method accepts a cancellation token
        var stored = await uploadStore.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken);
        return stored.StorageKey;
    }

    // goes through the real AddArtwork procedure, so this test also proves the
    // procedure names and column mappings line up between C# and SQL
    private async Task AddPaintingReferencing(string storageKey)
    {
        var catalog = new CatalogTestData(_database.DataSource);

        await catalog.AddPaintingAsync(
            "Sweeper test painting",
            termIds: [],
            seriesIds: [],
            images: [new ArtworkImage(storageKey, "test.jpg", 800, 600, BlurDataUri: null)]
        );
    }
}
