using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Vocabularies;

public record LoadedVocabulary(VocabularyWithShopItemTypes Vocabulary, VocabularyUsage Usage);
