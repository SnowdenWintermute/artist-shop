using ArtistShop.Web.Components.Pages.Admin.Catalog.Artworks;
using ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworksFromImages;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Components;

public sealed class ArtworkFromImagesRowTests
{
    private static ArtworkFromImagesRow Row() =>
        new(new ArtworkId(1), new PlannedArtworkFromImages(new ArtworkName("Dawn"), [], NewSeriesName: null, [new BulkImageCandidate("a", "/P/Dawn.jpg")]), []);

    // Firefox takes letters in a type="number" input and reports them as blank, which is why these are text
    [Fact]
    public void LettersInANumberFieldAreAnErrorRatherThanBlank()
    {
        var row = Row();

        row.YearText = "19x8";

        Assert.False(row.Validate());
        Assert.Equal("Enter the year as a number.", row.ErrorFor(nameof(ArtworkForm.YearCreated)));
    }

    [Fact]
    public void ACommaIsNotReadAsADecimalPoint()
    {
        var row = Row();

        row.HeightText = "12,5";
        row.WidthText = "30";

        Assert.False(row.Validate());
        Assert.Equal("Enter a number, like 12.5.", row.ErrorFor(nameof(ArtworkForm.HeightCm)));
        Assert.Null(row.ErrorFor(nameof(ArtworkForm.WidthCm)));
    }

    [Fact]
    public void AHeightAloneAsksForTheWidthUnderTheWidth()
    {
        var row = Row();

        row.HeightText = "12.5";

        Assert.False(row.Validate());
        Assert.Null(row.ErrorFor(nameof(ArtworkForm.HeightCm)));
        Assert.Equal("Enter a width too.", row.ErrorFor(nameof(ArtworkForm.WidthCm)));
    }

    [Fact]
    public void CorrectedTextClearsTheErrorAndSetsTheField()
    {
        var row = Row();
        row.HeightText = "abc";
        row.Validate();

        row.HeightText = " 12.5 ";
        row.WidthText = "30";

        Assert.True(row.Validate());
        Assert.Equal(12.5m, row.Form.HeightCm);
        Assert.Equal(30m, row.Form.WidthCm);
    }

    [Fact]
    public void ClearingANumberFieldEmptiesIt()
    {
        var row = Row();
        row.YearText = "1998";

        row.YearText = "";

        Assert.True(row.Validate());
        Assert.Null(row.Form.YearCreated);
    }
}
