using System.Security.Cryptography;
using ArtistShop.Web.Images;
using Microsoft.Extensions.Logging.Abstractions;
using NetVips;

namespace ArtistShop.Web.Tests.Images;

public sealed class ImageUploadStoreTests : IDisposable
{
    // as the app runs, where libvips can read SVG for brand images
    static ImageUploadStoreTests() => LibvipsSettings.Apply();

    private readonly DirectoryInfo _storageRoot;
    private readonly ImageStorage _imageStorage;

    public ImageUploadStoreTests()
    {
        _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
        _imageStorage = new ImageStorage(_storageRoot.FullName);
        _imageStorage.Create();
    }

    public void Dispose() => _storageRoot.Delete(recursive: true);

    private ImageUploadStore CreateStore(ImageProcessingLimiter limiter, ImageProcessingSettings settings) =>
        new(_imageStorage, new ImageProcessor(_imageStorage), limiter, settings, new ImmediateImageSizeQueue());

    private static MemoryStream CreateJpeg(int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3);
        return new MemoryStream(image.WriteToBuffer(".jpg"));
    }

    private void AssertNothingStored()
    {
        Assert.Empty(_imageStorage.ListOriginals());
        // no variant either, nor a folder for one
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(_storageRoot.FullName, "variants")));
    }

    [Fact]
    public async Task StoresAnImageAndItsVariants()
    {
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings);
        using var content = CreateJpeg(800, 600);

        var stored = await store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken);

        Assert.True(_imageStorage.OriginalExists(stored.StorageKey));
        Assert.Contains("800.webp", _imageStorage.VariantFileNames(stored.StorageKey));
        Assert.Equal(800, stored.Processed.Width);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content.ToArray())), stored.Sha256);
    }

    // the upload waits only for the smallest size; the rest go to the queue
    [Fact]
    public async Task HandsTheLaterSizesToTheQueue()
    {
        var queue = new BackgroundImageSizeQueue(NullLogger<BackgroundImageSizeQueue>.Instance);
        var store = new ImageUploadStore(
            _imageStorage,
            new ImageProcessor(_imageStorage),
            TestImageProcessing.CreateAmpleLimiter(),
            TestImageProcessing.Settings,
            queue
        );
        using var content = CreateJpeg(2000, 1500);

        var stored = await store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken);

        Assert.Equal(
            [ImageVariants.FileName(160, ImageVariantFormat.Avif), ImageVariants.FileName(160, ImageVariantFormat.Webp)],
            _imageStorage.VariantFileNames(stored.StorageKey).Order()
        );
        // marked before the upload answers, so a restart from here still makes the rest
        Assert.Equal([stored.StorageKey], _imageStorage.ListKeysWithSizesPending());
        var smallSizes = await queue.TakeAsync(TestContext.Current.CancellationToken);
        var largerSizes = await queue.TakeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(stored.StorageKey, smallSizes.StorageKey);
        Assert.Equal([400, 800, 1600], [.. smallSizes.Widths, .. largerSizes.Widths]);
    }

    [Fact]
    public async Task RejectsAndDeletesAnWorkImageNarrowerThanTheMinimum()
    {
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings);
        using var content = CreateJpeg(300, 200);

        await Assert.ThrowsAsync<ImageTooSmallException>(() =>
            store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken)
        );
        AssertNothingStored();
    }

    [Fact]
    public async Task StoresASmallPostImageWithACopyAtItsOwnWidth()
    {
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings);
        using var content = CreateJpeg(300, 200);

        var stored = await store.SaveAsync(content, ImageVariants.MinimumPostImageWidth, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["160.avif", "160.webp", "300.avif", "300.webp"],
            _imageStorage.VariantFileNames(stored.StorageKey).Order()
        );
    }

    [Fact]
    public async Task RejectsAndDeletesAnImageOverTheMegapixelLimit()
    {
        var settings = TestImageProcessing.Settings with { MaximumMegapixels = 1 };
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), settings);
        using var content = CreateJpeg(1500, 1000);

        await Assert.ThrowsAsync<ImageTooLargeException>(() =>
            store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken)
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
            store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken)
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
            store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken)
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
            store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken)
        );
        Assert.Equal(ImageProcessor.UnsupportedHeicMessage, exception.Message);
        AssertNothingStored();
    }

    // the JPEG recent iPhones take, with an HDR gain map that libvips opens with its own loader. Made
    // from a black image, with a blank gain map
    [Fact]
    public async Task StoresAnUltraHdrJpeg()
    {
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings);
        await using var content = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Images", "Fixtures", "ultra-hdr.jpg"));

        var stored = await store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken);

        Assert.Equal((800, 1067), (stored.Processed.Width, stored.Processed.Height));
        Assert.Contains("800.webp", _imageStorage.VariantFileNames(stored.StorageKey));
    }

    // only logos and icons may be SVG, whatever the browser called the file
    [Fact]
    public async Task TurnsAwayAndDeletesAnSvg()
    {
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings);
        using var content = new MemoryStream(
            """<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600"><rect width="800" height="600"/></svg>"""u8.ToArray()
        );

        var exception = await Assert.ThrowsAsync<UnsupportedImageFormatException>(() =>
            store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken)
        );
        Assert.Equal(ImageProcessor.UnacceptedFormatMessage, exception.Message);
        AssertNothingStored();
    }

    [Fact]
    public async Task DeletesAFileThatIsNotAnImage()
    {
        var store = CreateStore(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings);
        using var content = new MemoryStream("not an image"u8.ToArray());

        await Assert.ThrowsAsync<VipsException>(() =>
            store.SaveAsync(content, ImageVariants.MinimumSourceWidth, TestContext.Current.CancellationToken)
        );
        AssertNothingStored();
    }
}
