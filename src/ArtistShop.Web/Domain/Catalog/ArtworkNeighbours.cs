namespace ArtistShop.Web.Domain.Catalog;

// what it takes to link to an artwork: the name to show and the slug to point at
public record ArtworkLink(ArtworkName Name, ArtworkSlug Slug);

// an artwork at a place in a series, and which series, since that goes in its address. The image
// count is so that stepping back past an artwork's first image can land on the previous one's last
public record ArtworkInSeries(ArtworkSlug Slug, SeriesSlug SeriesSlug, int ImageCount);

// the places either side, running on into the series before or after; null on either side is the
// first place of the first series or the last of the last
public record ArtworkNeighbours(ArtworkInSeries? Previous, ArtworkInSeries? Next);
