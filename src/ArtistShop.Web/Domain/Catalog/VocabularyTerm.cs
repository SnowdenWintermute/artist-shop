namespace ArtistShop.Web.Domain.Catalog;

public record VocabularyId(int Value);

public record VocabularyName(string Value);

public record VocabularyTermId(int Value);

public record VocabularyTermName(string Value);

// a term as attached to a shop item. It carries its vocabulary's id and name so a
// painting can be displayed as "Medium: Acrylic" without looking the vocabulary up
public record VocabularyTerm(
    VocabularyTermId Id,
    VocabularyTermName Name,
    VocabularyId VocabularyId,
    VocabularyName VocabularyName
);
