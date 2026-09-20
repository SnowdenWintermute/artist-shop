namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Catalog;

// The repositories return vocabularies and terms unsorted, so every page that lists them puts
// them in the artist's reading order here
public static class VocabularyOrder
{
    private static readonly StringComparer ByName = StringComparer.CurrentCultureIgnoreCase;

    public static List<VocabularyWithTerms> SortedByName(
        IEnumerable<VocabularyWithTerms> vocabularies
    ) =>
        [
            .. vocabularies
                .OrderBy(vocabulary => vocabulary.Name.Value, ByName)
                .Select(vocabulary => vocabulary with
                {
                    Terms = [.. vocabulary.Terms.OrderBy(term => term.Name.Value, ByName)],
                }),
        ];
}
