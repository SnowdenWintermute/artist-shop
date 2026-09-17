using ArtistShop.Web.Images;
using NetVips;

namespace ArtistShop.Web.Tests.Images;

public sealed class ImageUploadStoreTests : IDisposable
{
    private readonly DirectoryInfo _storageRoot;
    private readonly ImageStorage _imageStorage;

    public ImageUploadStoreTests()
    {
        _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
        _imageStorage = new ImageStorage(_storageRoot.FullName);
        Directory.CreateDirectory(_imageStorage.Originals);
        Directory.CreateDirectory(_imageStorage.Variants);
    }

    public void Dispose() => _storageRoot.Delete(recursive: true);

    private ImageUploadStore CreateStore(ImageProcessingLimiter limiter, ImageProcessingSettings settings) =>
        new(_imageStorage, new ImageProcessor(_imageStorage), limiter, settings);

    private static MemoryStream CreateJpeg(int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3);
        return new MemoryStream(image.WriteToBuffer(".jpg"));
    }

    private void AssertNothingStored()
    {
        Assert.Empty(Directory.EnumerateFileSystemEntries(_imageStorage.Originals));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_imageStorage.Variants));
    }

    [Fact]
    public async Task StoresAnImageAndItsVariants()
    {
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings);
        using var content = CreateJpeg(800, 600);

        var stored = await store.SaveAsync(content, TestContext.Current.CancellationToken);

        Assert.True(_imageStorage.OriginalExists(stored.StorageKey));
        Assert.True(File.Exists(Path.Combine(_imageStorage.VariantDirectory(stored.StorageKey), "800.webp")));
        Assert.Equal(800, stored.Processed.Width);
    }

    [Fact]
    public async Task RejectsAndDeletesAnImageOverTheMegapixelLimit()
    {
        var settings = TestImageProcessing.Settings with { MaximumMegapixels = 1 };
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), settings);
        using var content = CreateJpeg(1500, 1000);

        await Assert.ThrowsAsync<ImageTooLargeException>(() =>
            store.SaveAsync(content, TestContext.Current.CancellationToken)
        );
        AssertNothingStored();
    }

    // 800 x 600 x 3 bytes x a multiplier of 2 is about 3 MB
    [Fact]
    public async Task RejectsAndDeletesAnImageOverTheMemoryBudget()
    {
        var limiter = new ImageProcessingLimiter(
            new ImageProcessingCapacity(ProcessorSlots: 1, MemoryBudgetMegabytes: 2, QueuedImageLimit: 0),
            TestImageProcessing.Settings.BusyRetryAfter
        );
        var store = CreateStore(limiter, TestImageProcessing.Settings);
        using var content = CreateJpeg(800, 600);

        await Assert.ThrowsAsync<ImageTooLargeException>(() =>
            store.SaveAsync(content, TestContext.Current.CancellationToken)
        );
        AssertNothingStored();
    }

    [Fact]
    public async Task DeletesAnImageTurnedAwayAsBusy()
    {
        var limiter = new ImageProcessingLimiter(
            new ImageProcessingCapacity(ProcessorSlots: 1, MemoryBudgetMegabytes: 100, QueuedImageLimit: 0),
            TestImageProcessing.Settings.BusyRetryAfter
        );
        var store = CreateStore(limiter, TestImageProcessing.Settings);
        using var content = CreateJpeg(800, 600);

        using var otherImage = await limiter.AcquireAsync(1, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ImageProcessingBusyException>(() =>
            store.SaveAsync(content, TestContext.Current.CancellationToken)
        );
        AssertNothingStored();
    }

    // a real iPhone-style HEIC, made with ImageMagick; the browser may label it image/jpeg
    [Fact]
    public async Task TurnsAwayAndDeletesAHeicPhoto()
    {
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings);
        await using var content = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Images", "Fixtures", "sample.heic"));

        var exception = await Assert.ThrowsAsync<UnsupportedImageFormatException>(() =>
            store.SaveAsync(content, TestContext.Current.CancellationToken)
        );
        Assert.Equal(ImageProcessor.UnsupportedHeicMessage, exception.Message);
        AssertNothingStored();
    }

    [Fact]
    public async Task DeletesAFileThatIsNotAnImage()
    {
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings);
        using var content = new MemoryStream("not an image"u8.ToArray());

        await Assert.ThrowsAsync<VipsException>(() =>
            store.SaveAsync(content, TestContext.Current.CancellationToken)
        );
        AssertNothingStored();
    }
}
