using ArtistShop.Web.Components.Catalog;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Components;

public sealed class TermChoicesTests
{
    private static readonly VocabularyId TimeOfDayId = new(1);
    private static readonly VocabularyName TimeOfDayName = new("Time of day");
    private static readonly VocabularyId MediumId = new(2);
    private static readonly VocabularyName MediumName = new("Medium");

    private static readonly VocabularyWithTerms TimeOfDay = new(
        TimeOfDayId,
        TimeOfDayName,
        IsSingleChoice: true,
        [
            new VocabularyTerm(new VocabularyTermId(11), new VocabularyTermName("Noon"), TimeOfDayId, TimeOfDayName),
            new VocabularyTerm(new VocabularyTermId(12), new VocabularyTermName("Midnight"), TimeOfDayId, TimeOfDayName),
        ]
    );

    private static readonly VocabularyWithTerms Medium = new(
        MediumId,
        MediumName,
        IsSingleChoice: false,
        [
            new VocabularyTerm(new VocabularyTermId(21), new VocabularyTermName("Oil"), MediumId, MediumName),
            new VocabularyTerm(new VocabularyTermId(22), new VocabularyTermName("Ink"), MediumId, MediumName),
        ]
    );

    [Fact]
    public void ChoosingReplacesOnlyThatVocabularysTerm()
    {
        HashSet<int> termIds = [11, 21];

        TermChoices.Choose(termIds, new TermChoice(TimeOfDay, 12));

        Assert.Equal([12, 21], termIds.Order());
        Assert.Equal(12, TermChoices.ChosenTermId(termIds, TimeOfDay));
    }

    [Fact]
    public void ChoosingNoneRemovesTheTerm()
    {
        HashSet<int> termIds = [11, 21];

        TermChoices.Choose(termIds, new TermChoice(TimeOfDay, TermId: null));

        Assert.Equal([21], termIds);
        Assert.Null(TermChoices.ChosenTermId(termIds, TimeOfDay));
    }

    [Fact]
    public void AddingReplacesSingleChoiceTermsAndKeepsOthers()
    {
        var added = TermChoices.WithAdded([TimeOfDay, Medium], [11, 21], new HashSet<int> { 12, 22 });

        Assert.Equal([12, 21, 22], added.Order());
    }

    [Fact]
    public void AddingToAnotherVocabularyKeepsTheSingleChoiceTerm()
    {
        var added = TermChoices.WithAdded([TimeOfDay, Medium], [11], new HashSet<int> { 22 });

        Assert.Equal([11, 22], added.Order());
    }
}
