namespace ArtistShop.Web.Components.Catalog;

using ArtistShop.Web.Domain.Catalog;

// a single-choice vocabulary's select changed: TermId is null for "None"
public record TermChoice(VocabularyWithTerms Vocabulary, int? TermId);

// Term ids are held as one set across every vocabulary, so a single-choice vocabulary's one term is
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

    // a term added to a single-choice vocabulary replaces the one the artwork had there
    public static HashSet<int> WithAdded(
        IEnumerable<VocabularyWithTerms> vocabularies,
        IEnumerable<int> termIds,
        IReadOnlySet<int> addedTermIds
    )
    {
        var replaced = vocabularies
            .Where(vocabulary => vocabulary.IsSingleChoice && vocabulary.Terms.Any(term => addedTermIds.Contains(term.Id.Value)))
            .SelectMany(vocabulary => vocabulary.Terms.Select(term => term.Id.Value))
            .ToHashSet();

        return [.. termIds.Where(id => !replaced.Contains(id)), .. addedTermIds];
    }
}
