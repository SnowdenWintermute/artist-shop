namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Artworks;

using ArtistShop.Web.Domain.Catalog;

// The page renders once before its first await has finished, so everything it draws is set
// together, in one field, or not at all
public record LoadedArtworkList(
    ArtworkListFilter Filter,
    ArtworkListPage Artworks,
    IReadOnlyList<ArtworkType> Types,
    IReadOnlyList<VocabularyWithTerms> Vocabularies,
    // in the artist's order
    IReadOnlyList<Series> Series
);
