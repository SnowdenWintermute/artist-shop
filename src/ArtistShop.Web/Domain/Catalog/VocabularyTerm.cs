namespace ArtistShop.Web.Domain.Catalog;

public record VocabularyTermId(int Value);

public record VocabularyTermName(string Value);

// a term as attached to an artwork. It carries its vocabulary's id and name so a
// artwork can be displayed as "Medium: Acrylic" without looking the vocabulary up
public record VocabularyTerm(
    VocabularyTermId Id,
    VocabularyTermName Name,
    VocabularyId VocabularyId,
    VocabularyName VocabularyName
);

public record VocabularyTermWithUsage(
    VocabularyTermId Id,
    VocabularyTermName Name,
    int ArtworkCount
);
