namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Works;

using ArtistShop.Web.Domain.Catalog;

// The page renders once before its first await has finished, so everything it draws is set
// together, in one field, or not at all
public record LoadedWorkList(
    WorkListFilter Filter,
    WorkListPage Works,
    IReadOnlyList<WorkType> Types,
    IReadOnlyList<VocabularyWithTerms> Vocabularies,
    // in the artist's order
    IReadOnlyList<Collection> Collections
);
