using System.Text;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Images;
using NetVips;

namespace ArtistShop.Web.Tests.Images;

public sealed class BrandImageProcessorTests
{
    // as the app runs, so SVG passes only because it's let through on purpose
    static BrandImageProcessorTests() => LibvipsSettings.Apply();

    private static readonly BrandImageProcessor Processor = new(
        TestImageProcessing.CreateAmpleLimiter(),
        TestImageProcessing.Settings
    );

    private const string Svg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="300" height="100" viewBox="0 0 300 100">
          <rect width="300" height="100" fill="#155dfc"/>
        </svg>
        """;

    private static Task<List<BrandImageFileContent>> ProcessAsync(byte[] original, BrandImageType type) =>
        Processor.ProcessAsync(original, type, TestContext.Current.CancellationToken);

    // an opaque white rectangle with a transparent margin round it
    private static byte[] PngWithMargin(int width, int height, int margin)
    {
        using var opaque = Image.Black(width, height, bands: 4).NewFromImage(255, 255, 255, 255);
        using var framed = opaque.Embed(margin, margin, width + 2 * margin, height + 2 * margin, background: [0, 0, 0, 0]);
        using var tagged = framed.Copy(interpretation: Enums.Interpretation.Srgb);
        return tagged.WriteToBuffer(".png");
    }

    private static byte[] Jpeg(int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3);
        return image.WriteToBuffer(".jpg");
    }

    [Fact]
    public async Task ALogoLosesItsTransparentMarginAndIsKeptAtThreeTimesTheBarHeight()
    {
        var files = await ProcessAsync(PngWithMargin(1200, 400, margin: 100), BrandImageType.Logo);

        var file = Assert.Single(files).File;
        Assert.Equal(BrandImageFileType.Raster, file.Type);
        Assert.Equal(BrandImageFormat.Webp, file.Format);
        Assert.Equal(BrandImageProcessor.LogoStoredHeight, file.Height);
        Assert.Equal(BrandImageProcessor.LogoStoredHeight * 3, file.Width);
    }

    [Fact]
    public async Task ALogoShorterThanThatIsNeverEnlarged()
    {
        var file = Assert.Single(await ProcessAsync(Jpeg(200, 60), BrandImageType.LogoOnDark)).File;

        Assert.Equal((200, 60), (file.Width, file.Height));
    }

    [Fact]
    public async Task ALogoLowerThanTheBarIsRefused()
    {
        await Assert.ThrowsAsync<UnusableImageException>(() => ProcessAsync(Jpeg(200, 40), BrandImageType.Logo));
    }

    [Fact]
    public async Task AFullyTransparentLogoIsRefused()
    {
        using var image = Image.Black(200, 100, bands: 4).Copy(interpretation: Enums.Interpretation.Srgb);

        await Assert.ThrowsAsync<UnusableImageException>(() =>
            ProcessAsync(image.WriteToBuffer(".png"), BrandImageType.Logo)
        );
    }

    [Fact]
    public async Task AnSvgLogoIsKeptAsUploaded()
    {
        var original = Encoding.UTF8.GetBytes(Svg);

        var content = Assert.Single(await ProcessAsync(original, BrandImageType.Logo));

        Assert.Equal(original, content.Bytes);
        Assert.Equal((BrandImageFormat.Svg, 300, 100), (content.File.Format, content.File.Width, content.File.Height));
    }

    [Fact]
    public async Task AFaviconGetsABrowserIconAndAnOpaqueHomeScreenIcon()
    {
        var files = await ProcessAsync(PngWithMargin(300, 100, margin: 0), BrandImageType.Favicon);

        Assert.Equal([BrandImageFileType.Raster, BrandImageFileType.AppleTouch], files.Select(content => content.File.Type));

        using var icon = Image.NewFromBuffer(files[0].Bytes);
        Assert.Equal((BrandImageProcessor.FaviconSize, BrandImageProcessor.FaviconSize), (icon.Width, icon.Height));
        // centred, never cropped: the top corner of a wide image is padding
        Assert.Equal(0, icon[3].Getpoint(0, 0)[0]);

        using var homeScreen = Image.NewFromBuffer(files[1].Bytes);
        Assert.Equal((BrandImageProcessor.AppleTouchSize, BrandImageProcessor.AppleTouchSize), (homeScreen.Width, homeScreen.Height));
        Assert.False(homeScreen.HasAlpha());
        Assert.Equal([255, 255, 255], homeScreen.Getpoint(0, 0));
    }

    [Fact]
    public async Task AnSvgFaviconKeepsItsSvgAndGetsBothPngs()
    {
        var files = await ProcessAsync(Encoding.UTF8.GetBytes(Svg), BrandImageType.Favicon);

        Assert.Equal(
            [BrandImageFileType.Svg, BrandImageFileType.Raster, BrandImageFileType.AppleTouch],
            files.Select(content => content.File.Type)
        );
    }

    // icons are often drawn on a 16 or 24 unit grid, and an SVG is sharp at any size
    [Fact]
    public async Task AnSvgFaviconSmallerThanTheBrowserIconIsAccepted()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16"><circle cx="8" cy="8" r="7"/></svg>""";

        var files = await ProcessAsync(Encoding.UTF8.GetBytes(svg), BrandImageType.Favicon);

        var icon = files.Single(content => content.File.Type == BrandImageFileType.Raster).File;
        Assert.Equal((BrandImageProcessor.FaviconSize, BrandImageProcessor.FaviconSize), (icon.Width, icon.Height));
    }

    [Fact]
    public async Task OtherFormatsLibvipsCanReadAreRefused()
    {
        using var image = Image.Black(100, 100, bands: 3);

        await Assert.ThrowsAsync<UnsupportedImageFormatException>(() =>
            ProcessAsync(image.WriteToBuffer(".gif"), BrandImageType.Favicon)
        );
    }
}
