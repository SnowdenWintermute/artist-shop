namespace ArtistShop.Web.Domain.Catalog;

public record VocabularyId(int Value);

public record VocabularyName(string Value);

public record Vocabulary(VocabularyId Id, VocabularyName Name);

// lists, not a set or dictionary: these are passed to interactive islands as JSON,
// and System.Text.Json can't read IReadOnlySet or dictionaries keyed by a record
public record VocabularyWithArtworkTypes(
    VocabularyId Id,
    VocabularyName Name,
    IReadOnlyList<ArtworkTypeId> ArtworkTypeIds
);

public record VocabularyWithTerms(
    VocabularyId Id,
    VocabularyName Name,
    IReadOnlyList<VocabularyTerm> Terms
);

public record ArtworkTypeUsage(ArtworkTypeId ArtworkTypeId, int ArtworkCount);

public record VocabularyUsage(
    int VocabularyTermCount,
    IReadOnlyList<ArtworkTypeUsage> ArtworkTypeUsages
)
{
    // types with no items using this vocabulary have no entry
    public int ArtworkCountFor(ArtworkTypeId artworkTypeId) =>
        ArtworkTypeUsages
            .FirstOrDefault(usage => usage.ArtworkTypeId == artworkTypeId)
            ?.ArtworkCount ?? 0;

    // each item has exactly one type, so no item is counted twice
    public int TotalArtworkCount => ArtworkTypeUsages.Sum(usage => usage.ArtworkCount);
}
