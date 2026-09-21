namespace ArtistShop.Web.Domain.Catalog;

// what it takes to link to an artwork: the name to show and the slug to point at
public record ArtworkLink(ArtworkName Name, ArtworkSlug Slug);

// null on either side is the end of the series
public record ArtworkNeighbours(ArtworkLink? Previous, ArtworkLink? Next);
