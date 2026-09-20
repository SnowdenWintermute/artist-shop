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

    // narrower than the smallest variant. ImageProcessor turns an upload like this away long
    // before here, against MinimumSourceWidth, which is wider still
    [Fact]
    public void AnImageNarrowerThanEveryVariantHasNone()
    {
        Assert.Throws<InvalidOperationException>(() => ImageVariants.LargestWidthUpTo(120, 800));
    }

    [Fact]
    public void BuildsTheVariantUrl()
    {
        Assert.Equal(
            "/media/0123abcd/400.webp",
            ImageUrls.Variant("0123abcd", imageWidth: 600, wantedWidth: 800, ImageVariantFormat.Webp)
        );
    }
}
