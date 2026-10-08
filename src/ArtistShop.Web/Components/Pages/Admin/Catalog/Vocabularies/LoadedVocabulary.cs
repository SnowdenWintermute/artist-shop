using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Vocabularies;

// WorksWithSeveralTerms is who'd lose their terms if the vocabulary were made mutually exclusive
public record LoadedVocabulary(
    VocabularyWithWorkTypes Vocabulary,
    VocabularyUsage Usage,
    IReadOnlyList<WorkName> WorksWithSeveralTerms
);
