namespace ArtistShop.Web.Components.Pages.Catalog;

using ArtistShop.Web.Catalog;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Images;

// One picture as the lightbox shows it: its largest file, which is as far as PhotoSwipe zooms, at
// the image's own shape, the srcset to choose a file from, and the address of the page it's on
public record WorkWalkImage(
    string Src,
    string Srcset,
    int Width,
    int Height,
    string Alt,
    string? Blur,
    string PageUrl
);

// A work's step in the walk Previous and Next take, for the lightbox to swipe through: its
// pictures, where to ask for the steps either side, and where its pictures sit among all of them.
// The work page writes its own step on the gallery, and the walk endpoint serves the others
public record WorkWalkStep(
    IReadOnlyList<WorkWalkImage> Images,
    string? PreviousWalkUrl,
    string? NextWalkUrl,
    int EarlierImageCount,
    int TotalImageCount
)
{
    public static WorkWalkStep For(Work work, WorkWalk walk)
    {
        var collectionSlug = walk.Collection?.Slug.Value;

        return new WorkWalkStep(
            [.. work.Images.Select((image, index) => ImageFor(work, collectionSlug, image, index))],
            walk.Neighbours.Previous is { } previous
                ? WorkWalkEndpoints.WalkUrl(previous.Slug.Value, previous.CollectionSlug.Value)
                : null,
            walk.Neighbours.Next is { } next ? WorkWalkEndpoints.WalkUrl(next.Slug.Value, next.CollectionSlug.Value) : null,
            walk.Position.EarlierImageCount,
            walk.Position.TotalImageCount
        );
    }

    private static WorkWalkImage ImageFor(Work work, string? collectionSlug, WorkImage image, int index)
    {
        var width = ImageVariants.LargestWidthUpTo(image.Width, ImageVariants.MainImageWidth);

        return new WorkWalkImage(
            ImageUrls.Variant(image.StorageKey, image.Width, ImageVariants.MainImageWidth, ImageVariantFormat.Avif),
            ImageUrls.SourceSet(image.StorageKey, image.Width, ImageVariants.MainImageWidth, ImageVariantFormat.Avif),
            width,
            (int)Math.Round((double)width * image.Height / image.Width),
            work.Name.Value,
            image.BlurDataUri,
            WorkPageQuery.ImageUrl(work.Slug.Value, collectionSlug, index)
        );
    }
}
