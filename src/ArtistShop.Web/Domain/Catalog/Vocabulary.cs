namespace ArtistShop.Web.Domain.Catalog;

public record VocabularyId(int Value);

public record VocabularyName(string Value);

public record Vocabulary(VocabularyId Id, VocabularyName Name);
