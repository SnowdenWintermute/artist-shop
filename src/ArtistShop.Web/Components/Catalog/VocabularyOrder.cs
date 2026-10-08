namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Catalog;

// The repositories return vocabularies and terms unsorted, so every page that lists them puts
// them in the artist's reading order here
public static class VocabularyOrder
{
    public static List<Vocabulary> SortedByName(IEnumerable<Vocabulary> vocabularies) =>
        [.. vocabularies.OrderBy(vocabulary => vocabulary.Name.Value, NameOrder.ByName)];

    public static List<VocabularyTermWithUsage> SortedByName(IEnumerable<VocabularyTermWithUsage> terms) =>
        [.. terms.OrderBy(term => term.Name.Value, NameOrder.ByName)];

    public static List<VocabularyWithTerms> SortedByName(
        IEnumerable<VocabularyWithTerms> vocabularies
    ) =>
        [
            .. vocabularies
                .OrderBy(vocabulary => vocabulary.Name.Value, NameOrder.ByName)
                .Select(vocabulary => vocabulary with
                {
                    Terms = [.. vocabulary.Terms.OrderBy(term => term.Name.Value, NameOrder.ByName)],
                }),
        ];

    public static List<VocabularyTermGroup> SortedByName(IEnumerable<VocabularyTermGroup> groups) =>
        [
            .. groups
                .OrderBy(group => group.VocabularyName.Value, NameOrder.ByName)
                .Select(group => group with
                {
                    Terms = [.. group.Terms.OrderBy(term => term.Name.Value, NameOrder.ByName)],
                }),
        ];

    // an artwork's terms arrive flat, each carrying its vocabulary, so they are gathered under
    // their vocabularies first and then put in the same order as everywhere else
    public static List<VocabularyTermGroup> GroupedByVocabulary(IEnumerable<VocabularyTerm> terms) =>
        SortedByName(
            terms
                .GroupBy(term => term.VocabularyId)
                .Select(group => new VocabularyTermGroup(group.Key, group.First().VocabularyName, [.. group]))
        );

    // of these term ids, the ones listed here, in the order they're listed, as a row shows its terms
    public static List<int> TermIdsInListOrder(IEnumerable<VocabularyWithTerms> vocabularies, IReadOnlySet<int> termIds) =>
        [.. vocabularies.SelectMany(vocabulary => vocabulary.Terms).Select(term => term.Id.Value).Where(termIds.Contains)];
}
