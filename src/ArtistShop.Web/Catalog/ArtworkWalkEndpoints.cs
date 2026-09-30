namespace ArtistShop.Web.Catalog;

using ArtistShop.Web.Components.Pages.Catalog;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Sites;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

// The steps either side of the artwork a lightbox opened on, fetched as the visitor swipes
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

        return TypedResults.Ok(ArtworkWalkStep.For(artwork, walk));
    }
}
