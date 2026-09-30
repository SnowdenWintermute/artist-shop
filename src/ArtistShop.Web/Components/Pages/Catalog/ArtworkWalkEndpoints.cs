namespace ArtistShop.Web.Components.Pages.Catalog;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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
// pictures, where to ask for the steps either side, and where its pictures sit among all of them
public record ArtworkWalkStep(
    IReadOnlyList<ArtworkWalkImage> Images,
    string? PreviousWalkUrl,
    string? NextWalkUrl,
    int EarlierImageCount,
    int TotalImageCount
);

public static class ArtworkWalkEndpoints
{
    public static void MapArtworkWalkEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/artworks/{slug}/walk", GetAsync).WithMetadata(new ServedOnAttribute(HostTypes.Site));

    public static string WalkUrl(string artworkSlug, string? seriesSlug) =>
        seriesSlug is null
            ? $"/artworks/{artworkSlug}/walk"
            : $"/artworks/{artworkSlug}/walk?{ArtworkPageQuery.SeriesKey}={seriesSlug}";

    // the same artwork and walk the page at that address shows
    private static async Task<Results<Ok<ArtworkWalkStep>, NotFound>> GetAsync(
        string slug,
        [FromQuery(Name = ArtworkPageQuery.SeriesKey)] string? seriesValue,
        ArtworkRepository artworks
    )
    {
        if (await artworks.GetBySlugAsync(slug) is not { } artwork)
        {
            return TypedResults.NotFound();
        }

        var walk = await ArtworkWalk.LoadAsync(artworks, artwork, seriesValue);

        return TypedResults.Ok(ToStep(artwork, walk));
    }

    public static ArtworkWalkStep ToStep(Artwork artwork, ArtworkWalk walk)
    {
        var seriesSlug = walk.Series?.Slug.Value;

        return new ArtworkWalkStep(
            [.. artwork.Images.Select((image, index) => ToImage(artwork, seriesSlug, image, index))],
            walk.Neighbours.Previous is { } previous ? WalkUrl(previous.Slug.Value, previous.SeriesSlug.Value) : null,
            walk.Neighbours.Next is { } next ? WalkUrl(next.Slug.Value, next.SeriesSlug.Value) : null,
            walk.Position.EarlierImageCount,
            walk.Position.TotalImageCount
        );
    }

    private static ArtworkWalkImage ToImage(Artwork artwork, string? seriesSlug, ArtworkImage image, int index)
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
