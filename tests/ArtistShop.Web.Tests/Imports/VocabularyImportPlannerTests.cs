using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;
using static ArtistShop.Web.Tests.Imports.CatalogSetupTestData;

namespace ArtistShop.Web.Tests.Imports;

public sealed class VocabularyImportPlannerTests
{
    private static VocabularyImportPlan Plan(string csv) => VocabularyImportPlanner.Plan(csv, ';', Snapshot());

    [Fact]
    public void AddsANewVocabularyWithItsTypesAndTerms()
    {
        var plan = Plan(
            """
            vocabulary,artworkTypes,terms
            Style,painting; Sculpture,Abstract; Figurative; abstract
            """
        );

        Assert.Empty(plan.Errors);
        var change = Assert.Single(plan.Changes);
        Assert.Null(change.ExistingId);
        Assert.Equal([Painting.Id, Sculpture.Id], change.AddedArtworkTypes.Select(type => type.Id));
        // a term listed twice is added once
        Assert.Equal([new VocabularyTermName("Abstract"), new VocabularyTermName("Figurative")], change.AddedTerms);
    }

    [Fact]
    public void AddsOnlyWhatAnExistingVocabularyIsMissing()
    {
        var plan = Plan(
            """
            vocabulary,artworkTypes,terms
            medium,Painting; Sculpture,oil; Bronze
            """
        );

        var change = Assert.Single(plan.Changes);
        Assert.Equal(MediumId, change.ExistingId);
        // its own spelling, so adding types doesn't rename it
        Assert.Equal(new VocabularyName("Medium"), change.Name);
        Assert.Equal([Painting.Id], change.ExistingArtworkTypes.Select(type => type.Id));
        Assert.Equal([Sculpture.Id], change.AddedArtworkTypes.Select(type => type.Id));
        Assert.Equal([new VocabularyTermName("Bronze")], change.AddedTerms);
    }

    [Fact]
    public void SkipsAVocabularyWithNothingMissing()
    {
        var plan = Plan(
            """
            vocabulary,artworkTypes,terms
            Medium,Painting,Oil
            """
        );

        Assert.Empty(plan.Changes);
        Assert.Equal(ImportSkipReason.AlreadyExists, Assert.Single(plan.SkippedRows).Reason);
    }

    // types come from their own import first
    [Fact]
    public void AnArtworkTypeThatDoesntExistIsAProblem()
    {
        var plan = Plan(
            """
            vocabulary,artworkTypes
            Style,Installation
            """
        );

        var error = Assert.Single(plan.Errors);
        Assert.Equal((2, "artworkTypes"), (error.RowNumber, error.Column));
        Assert.Empty(plan.Changes);
    }

    [Fact]
    public void AVocabularyNeedsAName()
    {
        var plan = Plan(
            """
            vocabulary,terms
            ,Oil
            """
        );

        Assert.Equal((2, "vocabulary"), (Assert.Single(plan.Errors).RowNumber, plan.Errors[0].Column));
    }

    [Fact]
    public void ANewVocabularyCanBeSingleChoice()
    {
        var plan = Plan(
            """
            vocabulary,artworkTypes,terms,singleChoice
            Time of day,Painting,Noon; Midnight,YES
            Style,Painting,Abstract,
            """
        );

        Assert.Empty(plan.Errors);
        Assert.Equal([true, false], plan.Changes.Select(change => change.IsSingleChoice));
    }

    [Fact]
    public void SingleChoiceMustBeYesOrNo()
    {
        var plan = Plan(
            """
            vocabulary,artworkTypes,terms,singleChoice
            Time of day,Painting,Noon,maybe
            """
        );

        Assert.Equal(VocabularyImportHeaders.SingleChoice, Assert.Single(plan.Errors).Column);
    }

    // the import only adds, and making it single-choice could take terms off artworks
    [Fact]
    public void AnExistingVocabularysSingleChoiceIsNotChanged()
    {
        var plan = Plan(
            """
            vocabulary,artworkTypes,terms,singleChoice
            Medium,Painting,Bronze,yes
            """
        );

        Assert.Empty(plan.Changes);
        Assert.Equal(VocabularyImportHeaders.SingleChoice, Assert.Single(plan.Errors).Column);
    }
}
