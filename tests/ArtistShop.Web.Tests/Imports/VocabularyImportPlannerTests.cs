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
            vocabulary,workTypes,terms
            Style,painting; Sculpture,Abstract; Figurative; abstract
            """
        );

        Assert.Empty(plan.Errors);
        var change = Assert.Single(plan.Changes);
        Assert.Null(change.ExistingId);
        Assert.Equal([Painting.Id, Sculpture.Id], change.AddedWorkTypes.Select(type => type.Id));
        // a term listed twice is added once
        Assert.Equal([new VocabularyTermName("Abstract"), new VocabularyTermName("Figurative")], change.AddedTerms);
    }

    [Fact]
    public void AddsOnlyWhatAnExistingVocabularyIsMissing()
    {
        var plan = Plan(
            """
            vocabulary,workTypes,terms
            medium,Painting; Sculpture,oil; Bronze
            """
        );

        var change = Assert.Single(plan.Changes);
        Assert.Equal(MediumId, change.ExistingId);
        // its own spelling, so adding types doesn't rename it
        Assert.Equal(new VocabularyName("Medium"), change.Name);
        Assert.Equal([Painting.Id], change.ExistingWorkTypes.Select(type => type.Id));
        Assert.Equal([Sculpture.Id], change.AddedWorkTypes.Select(type => type.Id));
        Assert.Equal([new VocabularyTermName("Bronze")], change.AddedTerms);
    }

    [Fact]
    public void SkipsAVocabularyWithNothingMissing()
    {
        var plan = Plan(
            """
            vocabulary,workTypes,terms
            Medium,Painting,Oil
            """
        );

        Assert.Empty(plan.Changes);
        Assert.Equal(ImportSkipReason.AlreadyExists, Assert.Single(plan.SkippedRows).Reason);
    }

    // types come from their own import first
    [Fact]
    public void AWorkTypeThatDoesntExistIsAProblem()
    {
        var plan = Plan(
            """
            vocabulary,workTypes
            Style,Installation
            """
        );

        var error = Assert.Single(plan.Errors);
        Assert.Equal((2, "workTypes"), (error.RowNumber, error.Column));
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
    public void ANewVocabularyCanBeMutuallyExclusive()
    {
        var plan = Plan(
            """
            vocabulary,workTypes,terms,mutuallyExclusive
            Time of day,Painting,Noon; Midnight,YES
            Style,Painting,Abstract,
            """
        );

        Assert.Empty(plan.Errors);
        Assert.Equal([true, false], plan.Changes.Select(change => change.IsMutuallyExclusive));
    }

    [Fact]
    public void MutuallyExclusiveMustBeYesOrNo()
    {
        var plan = Plan(
            """
            vocabulary,workTypes,terms,mutuallyExclusive
            Time of day,Painting,Noon,maybe
            """
        );

        Assert.Equal(VocabularyImportHeaders.MutuallyExclusive, Assert.Single(plan.Errors).Column);
    }

    // the import only adds, and making it mutually exclusive could take terms off works
    [Fact]
    public void AnExistingVocabularysMutuallyExclusiveIsNotChanged()
    {
        var plan = Plan(
            """
            vocabulary,workTypes,terms,mutuallyExclusive
            Medium,Painting,Bronze,yes
            """
        );

        Assert.Empty(plan.Changes);
        Assert.Equal(VocabularyImportHeaders.MutuallyExclusive, Assert.Single(plan.Errors).Column);
    }
}
