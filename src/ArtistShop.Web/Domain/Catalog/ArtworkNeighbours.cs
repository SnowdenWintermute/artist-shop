namespace ArtistShop.Web.Domain.Catalog;

// what it takes to link to an artwork: the name to show and the slug to point at
public record ArtworkLink(ArtworkName Name, ArtworkSlug Slug);

// the image count is so that stepping back past an artwork's first image can land on the previous
// one's last
public record ArtworkNeighbour(ArtworkName Name, ArtworkSlug Slug, int ImageCount);

// null on either side is the end of the series
public record ArtworkNeighbours(ArtworkNeighbour? Previous, ArtworkNeighbour? Next);

// an artwork in another series, and which one, since that series goes in its address
public record ArtworkInSeries(ArtworkSlug Slug, SeriesSlug SeriesSlug, int ImageCount);

// the last artwork of the series before and the first of the series after; null on either side is
// the first or last series
public record ArtworksBeyondSeries(ArtworkInSeries? Previous, ArtworkInSeries? Next);
