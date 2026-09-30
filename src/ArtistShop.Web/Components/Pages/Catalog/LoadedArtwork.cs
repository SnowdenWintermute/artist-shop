namespace ArtistShop.Web.Components.Pages.Catalog;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

// The page renders once before its first await has finished, so everything it draws is set
// together, in one field, or not at all. Mentions are the published posts that embed it, newest
// first
public record LoadedArtwork(
    Artwork Artwork,
    ArtworkWalk Walk,
    IReadOnlyList<PostMention> Mentions
);
