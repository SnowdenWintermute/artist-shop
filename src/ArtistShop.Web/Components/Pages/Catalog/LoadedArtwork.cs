namespace ArtistShop.Web.Components.Pages.Catalog;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

// The page renders once before its first await has finished, so everything it draws is set
// together, in one field, or not at all. Series is null when the artwork is in none. Beyond is
// only looked up at an end of the series, and is empty otherwise. Mentions are the published posts
// that embed it, newest first
public record LoadedArtwork(
    Artwork Artwork,
    Series? Series,
    ArtworkNeighbours Neighbours,
    ArtworksBeyondSeries Beyond,
    IReadOnlyList<PostMention> Mentions
);
