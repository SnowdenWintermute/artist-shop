using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Vocabularies;

// ArtworksWithSeveralTerms is who'd lose their terms if the vocabulary were made mutually exclusive
public record LoadedVocabulary(
    VocabularyWithArtworkTypes Vocabulary,
    VocabularyUsage Usage,
    IReadOnlyList<ArtworkName> ArtworksWithSeveralTerms
);
