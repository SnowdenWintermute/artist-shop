namespace ArtistShop.Web.Components.Pages.Catalog;

using ArtistShop.Web.Components.Catalog;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

// The walk Previous and Next take from an artwork: the series the visitor is going through, the
// places either side, and where the artwork's images sit among all of them. Loaded here for both
// the artwork page and the lightbox's walk endpoint, so the two never disagree. Series is null when
// the artwork is in none, and then the walk is its own images
public record ArtworkWalk(Series? Series, ArtworkNeighbours Neighbours, ArtworkWalkPosition Position)
{
    public static async Task<ArtworkWalk> LoadAsync(
        ArtworkRepository artworks,
        Artwork artwork,
        string? seriesValue
    )
    {
        if (SeriesFor(artwork, seriesValue) is not { } series)
        {
            return new(null, new ArtworkNeighbours(null, null), new ArtworkWalkPosition(0, artwork.Images.Count));
        }

        // a visitor is only sent on to work there is something to look at, which is the rule the
        // series page and the home page count by
        var neighbours = await artworks.GetNeighboursAsync(series.Id, artwork.Id, onlyArtworksWithImages: true);
        var position = await artworks.GetWalkPositionAsync(series.Id, artwork.Id);

        return new(series, neighbours, position);
    }

    // the series the visitor is walking through, when the artwork really is in it. Arriving cold,
    // or on a link to a series it has since left, still gets arrows: the first of its own, by name
    private static Series? SeriesFor(Artwork artwork, string? seriesValue) =>
        artwork.Series.FirstOrDefault(series => series.Slug.Value == seriesValue)
        ?? SeriesOrder.SortedByName(artwork.Series).FirstOrDefault();
}
