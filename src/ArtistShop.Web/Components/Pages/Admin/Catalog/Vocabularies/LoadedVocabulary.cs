using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Vocabularies;

public record LoadedVocabulary(VocabularyWithArtworkTypes Vocabulary, VocabularyUsage Usage);
