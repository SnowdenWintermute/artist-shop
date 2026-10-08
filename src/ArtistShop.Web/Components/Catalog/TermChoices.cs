namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Catalog;

// a mutually exclusive vocabulary's select changed: TermId is null for "None"
public record TermChoice(VocabularyWithTerms Vocabulary, int? TermId);

// Term ids are held as one set across every vocabulary, so a mutually exclusive vocabulary's one term is
// found and replaced within it here
public static class TermChoices
{
    public static int? ChosenTermId(IReadOnlySet<int> termIds, VocabularyWithTerms vocabulary)
    {
        foreach (var term in vocabulary.Terms)
        {
            if (termIds.Contains(term.Id.Value))
            {
                return term.Id.Value;
            }
        }

        return null;
    }

    public static void Choose(ISet<int> termIds, TermChoice choice)
    {
        termIds.ExceptWith(choice.Vocabulary.Terms.Select(term => term.Id.Value));

        if (choice.TermId is { } termId)
        {
            termIds.Add(termId);
        }
    }

    // A vocabulary made mutually exclusive in another tab can leave several of its terms chosen here.
    // All of them go, as update_vocabulary took them off the work, rather than the select quietly
    // keeping whichever it shows
    public static void DropSeveralInMutuallyExclusive(ISet<int> termIds, IEnumerable<VocabularyWithTerms> vocabularies)
    {
        foreach (var vocabulary in vocabularies.Where(vocabulary => vocabulary.IsMutuallyExclusive))
        {
            var vocabularyTermIds = vocabulary.Terms.Select(term => term.Id.Value).ToList();

            if (vocabularyTermIds.Count(termIds.Contains) > 1)
            {
                termIds.ExceptWith(vocabularyTermIds);
            }
        }
    }

    // a term added to a mutually exclusive vocabulary replaces the one the work had there
    public static HashSet<int> WithAdded(
        IEnumerable<VocabularyWithTerms> vocabularies,
        IEnumerable<int> termIds,
        IReadOnlySet<int> addedTermIds
    )
    {
        var replaced = vocabularies
            .Where(vocabulary => vocabulary.IsMutuallyExclusive && vocabulary.Terms.Any(term => addedTermIds.Contains(term.Id.Value)))
            .SelectMany(vocabulary => vocabulary.Terms.Select(term => term.Id.Value))
            .ToHashSet();

        return [.. termIds.Where(id => !replaced.Contains(id)), .. addedTermIds];
    }
}
