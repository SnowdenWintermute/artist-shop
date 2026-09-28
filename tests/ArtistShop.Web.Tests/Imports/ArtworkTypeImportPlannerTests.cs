using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;
using static ArtistShop.Web.Tests.Imports.CatalogSetupTestData;

namespace ArtistShop.Web.Tests.Imports;

public sealed class ArtworkTypeImportPlannerTests
{
    private static ArtworkTypeImportPlan Plan(string csv) => ArtworkTypeImportPlanner.Plan(csv, ';', Snapshot());

    [Fact]
    public void AddsANewTypeWithItsFields()
    {
        var plan = Plan(
            """
            artworkType,fields
            Installation,date created; Height and width; Depth
            Video,Duration
            """
        );

        Assert.Empty(plan.Errors);
        Assert.Equal(2, plan.ChangeCount);
        Assert.Equal(new ArtworkTypeName("Installation"), plan.Additions[0].Name);
        Assert.Equal([ArtworkField.DateCreated, ArtworkField.HeightAndWidth, ArtworkField.Depth], plan.Additions[0].Fields);
    }

    [Fact]
    public void ATypeWithNoFieldsColumnHasNoFields()
    {
        var plan = Plan(
            """
            artworkType
            Doodle
            """
        );

        Assert.Empty(Assert.Single(plan.Additions).Fields);
    }

    // its fields aren't changed or checked, as a skipped artwork row isn't
    [Fact]
    public void SkipsATypeThatExists()
    {
        var plan = Plan(
            """
            artworkType,fields
            painting,Nonsense
            """
        );

        Assert.Empty(plan.Errors);
        Assert.Equal(ImportSkipReason.AlreadyExists, Assert.Single(plan.SkippedRows).Reason);
        Assert.False(((IImportPlan)plan).CanImport);
    }

    [Fact]
    public void SkipsANameOnMoreThanOneRow()
    {
        var plan = Plan(
            """
            artworkType,fields
            Video,Duration
            video,
            """
        );

        Assert.All(plan.SkippedRows, skipped => Assert.Equal(ImportSkipReason.RepeatedInFile, skipped.Reason));
        Assert.Equal(2, plan.SkippedRows.Count);
    }

    [Fact]
    public void AnUnknownFieldIsAProblem()
    {
        var plan = Plan(
            """
            artworkType,fields
            Video,Length
            """
        );

        var error = Assert.Single(plan.Errors);
        Assert.Equal((2, "fields"), (error.RowNumber, error.Column));
    }

    [Fact]
    public void DepthWithoutHeightAndWidthIsAProblem()
    {
        var plan = Plan(
            """
            artworkType,fields
            Relief,Depth
            """
        );

        Assert.Contains("\"Height and width\"", Assert.Single(plan.Errors).Message);
    }

    [Fact]
    public void AnUnknownColumnIsAProblem()
    {
        var plan = Plan(
            """
            artworkType,colour
            Video,Red
            """
        );

        Assert.Equal((1, "colour"), (Assert.Single(plan.Errors).RowNumber, plan.Errors[0].Column));
    }
}
