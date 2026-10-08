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

// an artwork's terms in one vocabulary
public record VocabularyTermGroup(
    VocabularyId VocabularyId,
    VocabularyName VocabularyName,
    IReadOnlyList<VocabularyTerm> Terms
);

public record VocabularyTermWithUsage(
    VocabularyTermId Id,
    VocabularyTermName Name,
    int ArtworkCount
);
