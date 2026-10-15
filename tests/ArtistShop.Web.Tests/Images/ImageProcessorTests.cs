using ArtistShop.Web.Images;
using NetVips;

namespace ArtistShop.Web.Tests.Images;

public sealed class ImageProcessorTests : IDisposable
{
    static ImageProcessorTests() => LibvipsSettings.Apply();

    private readonly DirectoryInfo _storageRoot;
    private readonly ImageStorage _imageStorage;
    private readonly ImageProcessor _processor;

    public ImageProcessorTests()
    {
        _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
        _imageStorage = new ImageStorage(_storageRoot.FullName);
        _imageStorage.Create();
        _processor = new ImageProcessor(_imageStorage);
    }

    public void Dispose() => _storageRoot.Delete(recursive: true);

    private async Task<string> SaveJpegAsync(int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3);
        var storageKey = ImageStorage.NewStorageKey();
        await _imageStorage.SaveOriginalAsync(new MemoryStream(image.WriteToBuffer(".jpg")), storageKey, TestContext.Current.CancellationToken);

        return storageKey;
    }

    private List<string> SizeFileNames(IEnumerable<int> widths) =>
        [.. widths.SelectMany(width => new[] { ImageVariants.FileName(width, ImageVariantFormat.Avif), ImageVariants.FileName(width, ImageVariantFormat.Webp) }).Order()];

    [Fact]
    public async Task TheFirstSizeIsOnlyTheSmallestAndTheBlur()
    {
        var storageKey = await SaveJpegAsync(2000, 1500);

        var processed = _processor.MakeFirstSize(storageKey, ImageVariants.MinimumSourceWidth);

        Assert.Equal(SizeFileNames([ImageVariants.FirstWidth(2000)]), _imageStorage.VariantFileNames(storageKey).Order());
        Assert.Equal((2000, 1500), (processed.Width, processed.Height));
        Assert.StartsWith("data:image/webp;base64,", processed.BlurDataUri);
    }

    [Fact]
    public async Task TheLaterSizesAreMadeAfterwards()
    {
        var storageKey = await SaveJpegAsync(2000, 1500);

        _processor.MakeFirstSize(storageKey, ImageVariants.MinimumSourceWidth);
        _processor.MakeSizes(storageKey, ImageVariants.LaterWidths(2000));

        Assert.Equal(SizeFileNames(ImageVariants.WidthsFor(2000)), _imageStorage.VariantFileNames(storageKey).Order());
    }

    // each size keeps the image's shape, whether made from the original or from the size before it
    [Fact]
    public async Task EverySizeKeepsTheImagesShape()
    {
        var storageKey = await SaveJpegAsync(2000, 1000);

        _processor.MakeSizes(storageKey, [400, 800, 1600]);

        foreach (var width in new[] { 400, 800, 1600 })
        {
            var stored = _imageStorage.OpenVariant(storageKey, ImageVariants.FileName(width, ImageVariantFormat.Webp));
            Assert.NotNull(stored);
            await using var content = stored.Content;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, TestContext.Current.CancellationToken);
            using var size = Image.NewFromBuffer(buffer.ToArray());
            Assert.Equal((width, width / 2), (size.Width, size.Height));
        }
    }
}
