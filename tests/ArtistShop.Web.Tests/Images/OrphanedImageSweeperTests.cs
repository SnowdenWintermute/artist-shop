using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Images;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetVips;

namespace ArtistShop.Web.Tests.Images;

public sealed class OrphanedImageSweeperTests : IClassFixture<TestDatabaseFixture>, IDisposable
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    private readonly TestDatabaseFixture _database;
    private readonly DirectoryInfo _storageRoot;
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
        _imageStorage = new ImageStorage(_storageRoot.FullName);
        _uploadStore = new ImageUploadStore(_imageStorage, new ImageProcessor(_imageStorage));
        Directory.CreateDirectory(_imageStorage.Originals);
        Directory.CreateDirectory(_imageStorage.Variants);

        var startTime = DateTimeOffset.UtcNow;
        _time = new FakeTimeProvider(startTime);

        _sweeper = new OrphanedImageSweeper(
            _imageStorage,
            new ShopItemImageRepository(database.ConnectionFactory),
            new OrphanedImageSweepSettings(GracePeriod, Interval: TimeSpan.FromDays(1)),
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
        await _sweeper.SweepAsync();

        Assert.False(_imageStorage.OriginalExists(storageKey));
        Assert.False(Directory.Exists(_imageStorage.VariantDirectory(storageKey)));
    }

    [Fact]
    public async Task KeepsUnreferencedUploadInsideGracePeriod()
    {
        var storageKey = await UploadImageAsync();

        _time.Advance(GracePeriod - TimeSpan.FromMinutes(1));
        await _sweeper.SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
        Assert.True(Directory.Exists(_imageStorage.VariantDirectory(storageKey)));
    }

    [Fact]
    public async Task KeepsReferencedUploadOlderThanGracePeriod()
    {
        var storageKey = await UploadImageAsync();
        await AddPaintingReferencing(storageKey);

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await _sweeper.SweepAsync();

        Assert.True(_imageStorage.OriginalExists(storageKey));
        Assert.True(Directory.Exists(_imageStorage.VariantDirectory(storageKey)));
    }

    [Fact]
    public async Task LeavesFilesNotNamedLikeUploadsAlone()
    {
        var strayFilePath = Path.Combine(_imageStorage.Originals, "notes.txt");
        File.WriteAllText(strayFilePath, "not an upload");

        _time.Advance(GracePeriod + TimeSpan.FromMinutes(1));
        await _sweeper.SweepAsync();

        Assert.True(File.Exists(strayFilePath));
    }

    private async Task<string> UploadImageAsync()
    {
        // NetVips can create an image from nothing. Black, 800 by 600, 3 colour channels (red,
        // green, blue) like a real photo: wide enough to get variants
        using var image = Image.Black(800, 600, bands: 3);
        using var content = new MemoryStream(image.WriteToBuffer(".jpg"));

        // TestContext.Current.CancellationToken is cancelled if the test run is stopped or times
        // out; xUnit's analyzer asks for it wherever a method accepts a cancellation token
        var stored = await _uploadStore.SaveAsync(content, TestContext.Current.CancellationToken);
        return stored.StorageKey;
    }

    // goes through the real AddPainting procedure, so this test also proves the
    // procedure names and column mappings line up between C# and SQL
    private async Task AddPaintingReferencing(string storageKey)
    {
        var repository = new PaintingRepository(_database.ConnectionFactory);

        await repository.AddAsync(
            new PaintingCatalogAddition(
                new ShopItemName("Sweeper test painting"),
                ShopItemSlug.FromName("Sweeper test painting"),
                Price: 100m,
                Stock: 1,
                DatePainted: new PartialDate(new DateOnly(2026, 1, 1), DatePrecision.Day),
                Description: null,
                Dimensions: null,
                Images: [new ShopItemImage(storageKey, "test.jpg", 800, 600, BlurDataUri: null)],
                MainImageIndex: 0,
                MediumIds: [],
                SupportIds: [],
                SeriesIds: []
            )
        );
    }
}
