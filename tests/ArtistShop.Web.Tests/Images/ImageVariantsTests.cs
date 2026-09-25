using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.Images;

public sealed class ImageVariantsTests
{
    [Theory]
    [InlineData(4000, 800, 800)]
    [InlineData(4000, 1000, 800)]
    [InlineData(4000, 5000, 1600)]
    // a 600-wide image has no variant above 400
    [InlineData(600, 800, 400)]
    // nothing is as small as wanted, so the smallest there is
    [InlineData(4000, 100, 160)]
    public void PicksTheLargestVariantThatExistsUpToTheWantedWidth(int imageWidth, int wantedWidth, int expected)
    {
        Assert.Equal(expected, ImageVariants.LargestWidthUpTo(imageWidth, wantedWidth));
    }

    // only a post takes an image this narrow
    [Theory]
    [InlineData(120, new[] { 120 })]
    [InlineData(160, new[] { 160 })]
    [InlineData(300, new[] { 160, 300 })]
    [InlineData(400, new[] { 160, 400 })]
    [InlineData(1000, new[] { 160, 400, 800 })]
    public void AnImageNarrowerThanTheMediumEmbedAlsoHasItsOwnWidth(int imageWidth, int[] expected)
    {
        Assert.Equal(expected, ImageVariants.WidthsFor(imageWidth));
    }

    // the rule PostImageEmbed.razor.js decides whether the lightbox checkbox is on by
    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    [InlineData(160)]
    [InlineData(300)]
    [InlineData(399)]
    [InlineData(400)]
    [InlineData(799)]
    [InlineData(800)]
    [InlineData(5000)]
    public void AnImagesWidestFileIsItsOwnWidthUnderMediumAndOtherwiseTheWidestStandardOneItReaches(int imageWidth)
    {
        var expected =
            imageWidth < ImageVariants.EmbedWidth(EmbedImageSize.Medium)
                ? imageWidth
                : ImageVariants.Widths.Where(width => width <= imageWidth).Max();

        Assert.Equal(expected, ImageVariants.LargestWidthFor(imageWidth));
    }

    // the rule PostImageEmbed.razor.js picks a post image's file by
    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    [InlineData(160)]
    [InlineData(300)]
    [InlineData(400)]
    [InlineData(5000)]
    public void APostImagesEmbedShowsTheNarrowerOfItsSizeAndItsOwnWidth(int imageWidth)
    {
        foreach (var size in Enum.GetValues<EmbedImageSize>())
        {
            var embedWidth = ImageVariants.EmbedWidth(size);
            Assert.Equal(Math.Min(embedWidth, imageWidth), ImageVariants.LargestWidthUpTo(imageWidth, embedWidth));
        }
    }

    [Fact]
    public void BuildsTheVariantUrl()
    {
        Assert.Equal(
            "/media/0123abcd/400.webp",
            ImageUrls.Variant("0123abcd", imageWidth: 600, wantedWidth: 800, ImageVariantFormat.Webp)
        );
    }

    // the endpoint that serves variants reads back the names ImageProcessor writes
    [Theory]
    [InlineData(ImageVariantFormat.Avif)]
    [InlineData(ImageVariantFormat.Webp)]
    public void ReadsBackTheFormatOfAVariantsFileName(ImageVariantFormat format)
    {
        Assert.Equal(format, ImageVariants.ReadFileName(ImageVariants.FileName(800, format)));
    }

    [Theory]
    [InlineData("800.png")]
    [InlineData("800.AVIF")]
    [InlineData("avif")]
    [InlineData("large.avif")]
    public void ReadsNoFormatFromAnyOtherName(string fileName)
    {
        Assert.Null(ImageVariants.ReadFileName(fileName));
    }
}
