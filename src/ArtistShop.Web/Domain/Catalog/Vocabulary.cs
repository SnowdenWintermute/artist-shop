namespace ArtistShop.Web.Domain.Catalog;

public record VocabularyId(int Value);

public record VocabularyName(string Value);

public record Vocabulary(VocabularyId Id, VocabularyName Name);

// lists, not a set or dictionary: these are passed to interactive islands as JSON,
// and System.Text.Json can't read IReadOnlySet or dictionaries keyed by a record
// a mutually exclusive vocabulary allows a work at most one of its terms
public record VocabularyWithWorkTypes(
    VocabularyId Id,
    VocabularyName Name,
    bool IsMutuallyExclusive,
    IReadOnlyList<WorkTypeId> WorkTypeIds
);

public record VocabularyWithTerms(
    VocabularyId Id,
    VocabularyName Name,
    bool IsMutuallyExclusive,
    IReadOnlyList<VocabularyTerm> Terms
);

public record WorkTypeUsage(WorkTypeId WorkTypeId, int WorkCount);

public record VocabularyUsage(
    int VocabularyTermCount,
    IReadOnlyList<WorkTypeUsage> WorkTypeUsages
)
{
    // types with no items using this vocabulary have no entry
    public int WorkCountFor(WorkTypeId workTypeId) =>
        WorkTypeUsages
            .FirstOrDefault(usage => usage.WorkTypeId == workTypeId)
            ?.WorkCount ?? 0;

    // each item has exactly one type, so no item is counted twice
    public int TotalWorkCount => WorkTypeUsages.Sum(usage => usage.WorkCount);
}
