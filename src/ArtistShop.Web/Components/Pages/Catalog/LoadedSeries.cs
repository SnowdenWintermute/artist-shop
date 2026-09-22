namespace ArtistShop.Web.Components.Pages.Catalog;

using ArtistShop.Web.Domain.Catalog;

// The page renders once before its first await has finished, so everything it draws is set
// together, in one field, or not at all
public record LoadedSeries(Series Series, ArtworkListPage Artworks, ArtworkImage? Cover);
