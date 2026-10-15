using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Domain.Website;
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
        _imageStorage.Create();

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
        Assert.Empty(_imageStorage.VariantFileNames(storageKey));
    }

    // as the size queue leaves an image deleted while its sizes were being made
    [Fact]
    public async Task RemovesSizesWhoseOriginalIsGoneAtOnce()
    {
        var storageKey = ImageStorage.NewStorageKey();
        _imageStorage.SaveVariant(storageKey, ImageVariants.FileName(800, ImageVariantFormat.Webp), "a size"u8);
        _imageStorage.MarkSizesPending(storageKey);

        await SweepAsync();

        Assert.DoesNotContain(storageKey, _imageStorage.ListKeysWithVariants());
        Assert.DoesNotContain(storageKey, _imageStorage.ListKeysWithSizesPending());
    }

    [Fact]
    public async Task KeepsUnreferencedUploadInsideGracePeriod()
    {
        var storageKey = await UploadImageAsync();

        _time.Advance(GracePeriod - TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
        Assert.NotEmpty(_imageStorage.VariantFileNames(storageKey));
    }

    [Fact]
    public async Task KeepsReferencedUploadOlderThanGracePeriod()
    {
        var storageKey = await UploadImageAsync();
        await AddPaintingReferencing(storageKey);

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
        Assert.NotEmpty(_imageStorage.VariantFileNames(storageKey));
    }

    [Fact]
    public async Task KeepsUploadAPostNamesOlderThanGracePeriod()
    {
        var storageKey = await UploadImageAsync();
        await new PostRepository(_database.Site).AddAsync(
            new PostTitle($"Sweeper test post {storageKey}"),
            PostSlug.FromTitle($"Sweeper test post {storageKey}"),
            new RichText(
                """{"ops":[{"insert":{"artshop-image":{"storageKey":"KEY","width":800,"height":600}}},{"insert":"\n"}]}"""
                    .Replace("KEY", storageKey)
            ),
            PostStatus.Draft
        );

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
        Assert.NotEmpty(_imageStorage.VariantFileNames(storageKey));
    }

    [Fact]
    public async Task KeepsUploadAnArticleNamesOlderThanGracePeriod()
    {
        var storageKey = await UploadImageAsync();
        var pages = new CustomPageRepository(_database.Site);
        var pageId = await pages.AddAsync($"Sweeper test page {storageKey}", new CustomPageAddress($"sweeper-test-{storageKey}"));
        await new PageRowRepository(_database.Site).AddAsync(pageId);
        var slot = Assert.Single(Assert.Single((await pages.GetLayoutAsync(pageId)).Rows).Slots);
        await new CustomComponentRepository(_database.Site).AddToSlotAsync(
            slot.Id,
            $"Sweeper test text {storageKey}",
            new TextSettings(
                new RichText(
                    """{"ops":[{"insert":{"artshop-image":{"storageKey":"KEY","width":800,"height":600}}},{"insert":"\n"}]}"""
                        .Replace("KEY", storageKey)
                ),
                TextWidth.Narrow
            )
        );

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
    }

    [Fact]
    public async Task KeepsABrandImageOriginalOlderThanGracePeriod()
    {
        var brandImageRepository = new BrandImageRepository(_database.Site);
        var store = new BrandImageStore(
            brandImageRepository,
            _imageStorage,
            new BrandImageProcessor(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings),
            NullLogger<BrandImageStore>.Instance
        );
        using var image = Image.Black(200, 100, bands: 3);
        using var content = new MemoryStream(image.WriteToBuffer(".png"));
        await store.ReplaceAsync(BrandImageType.Logo, content, "logo.png", TestContext.Current.CancellationToken);
        var storageKey = Assert.Single(
            await brandImageRepository.GetAllStorageKeysAsync(),
            key => _imageStorage.OriginalExists(key)
        );

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
    }

    [Fact]
    public async Task LeavesFilesNotNamedLikeUploadsAlone()
    {
        var strayFilePath = Path.Combine(_storageRoot.FullName, "sites", "1", "originals", "notes.txt");
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
        otherSiteStorage.Create();
        var otherSiteKey = await UploadImageAsync(UploadStoreFor(otherSiteStorage));

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await SweepAsync();

        Assert.True(otherSiteStorage.OriginalExists(otherSiteKey));
        Assert.NotEmpty(otherSiteStorage.VariantFileNames(otherSiteKey));
    }

    private Task SweepAsync() =>
        _sweeper.SweepAsync(
            _imageStorage,
            new WorkImageRepository(_database.Site),
            new PostRepository(_database.Site),
            new CustomComponentRepository(_database.Site),
            new BrandImageRepository(_database.Site)
        );

    private static ImageUploadStore UploadStoreFor(ImageStorage imageStorage) =>
        new(
            imageStorage,
            new ImageProcessor(imageStorage),
            TestImageProcessing.CreateAmpleLimiter(),
            TestImageProcessing.Settings,
            new ImmediateImageSizeQueue()
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

    // goes through the real AddWork procedure, so this test also proves the
    // procedure names and column mappings line up between C# and SQL
    private async Task AddPaintingReferencing(string storageKey)
    {
        var catalog = new CatalogTestData(_database.Site);

        await catalog.AddPaintingAsync(
            "Sweeper test painting",
            termIds: [],
            collectionIds: [],
            images: [new WorkImage(storageKey, "test.jpg", 800, 600, BlurDataUri: null)]
        );
    }
}
