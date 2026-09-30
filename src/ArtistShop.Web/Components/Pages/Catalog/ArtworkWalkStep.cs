namespace ArtistShop.Web.Components.Pages.Catalog;

using ArtistShop.Web.Catalog;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Images;

// One picture as the lightbox shows it: its largest file, which is as far as PhotoSwipe zooms, at
// the image's own shape, the srcset to choose a file from, and the address of the page it's on
public record ArtworkWalkImage(
    string Src,
    string Srcset,
    int Width,
    int Height,
    string Alt,
    string? Blur,
    string PageUrl
);

// An artwork's step in the walk Previous and Next take, for the lightbox to swipe through: its
// pictures, where to ask for the steps either side, and where its pictures sit among all of them.
// The artwork page writes its own step on the gallery, and the walk endpoint serves the others
public record ArtworkWalkStep(
    IReadOnlyList<ArtworkWalkImage> Images,
    string? PreviousWalkUrl,
    string? NextWalkUrl,
    int EarlierImageCount,
    int TotalImageCount
)
{
    public static ArtworkWalkStep For(Artwork artwork, ArtworkWalk walk)
    {
        var seriesSlug = walk.Series?.Slug.Value;

        return new ArtworkWalkStep(
            [.. artwork.Images.Select((image, index) => ImageFor(artwork, seriesSlug, image, index))],
            walk.Neighbours.Previous is { } previous
                ? ArtworkWalkEndpoints.WalkUrl(previous.Slug.Value, previous.SeriesSlug.Value)
                : null,
            walk.Neighbours.Next is { } next ? ArtworkWalkEndpoints.WalkUrl(next.Slug.Value, next.SeriesSlug.Value) : null,
            walk.Position.EarlierImageCount,
            walk.Position.TotalImageCount
        );
    }

    private static ArtworkWalkImage ImageFor(Artwork artwork, string? seriesSlug, ArtworkImage image, int index)
    {
        var width = ImageVariants.LargestWidthUpTo(image.Width, ImageVariants.MainImageWidth);

        return new ArtworkWalkImage(
            ImageUrls.Variant(image.StorageKey, image.Width, ImageVariants.MainImageWidth, ImageVariantFormat.Avif),
            ImageUrls.SourceSet(image.StorageKey, image.Width, ImageVariants.MainImageWidth, ImageVariantFormat.Avif),
            width,
            (int)Math.Round((double)width * image.Height / image.Width),
            artwork.Name.Value,
            image.BlurDataUri,
            ArtworkPageQuery.ImageUrl(artwork.Slug.Value, seriesSlug, index)
        );
    }
}
