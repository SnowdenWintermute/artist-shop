using ArtistShop.Web.Components.Pages.Admin.Catalog.Artworks;
using ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkTable;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Components;

public sealed class ArtworkTableRowTests
{
    private static ArtworkTableRow Row() =>
        ArtworkTableRow.FromDrop(new ArtworkId(1), new PlannedArtworkFromImages(new ArtworkName("Dawn"), [], NewSeriesName: null, [new BulkImageCandidate("a", "/P/Dawn.jpg")]), []);

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

    private static ArtworkImage Image(char letter) =>
        new(new string(letter, 32), OriginalFileName: null, Width: 800, Height: 600, BlurDataUri: null);

    private static Artwork SavedArtwork() =>
        new(
            new ArtworkId(2),
            new ArtworkType(new ArtworkTypeId(1), new ArtworkTypeName("Painting")),
            new ArtworkName("Dusk"),
            new ArtworkSlug("dusk"),
            description: null,
            new PartialDate(new DateOnly(1998, 1, 1), DatePrecision.Year),
            new DimensionsCentimeters(new Dimensions(12.5m, 30m, depth: null)),
            duration: null,
            [Image('a'), Image('b')],
            series: [],
            vocabularyTerms: [],
            products: []
        )
        {
            MainImageIndex = 1,
        };

    // the text boxes show what's saved, so leaving one unchanged can't blank it
    [Fact]
    public void ASavedArtworkFillsTheNumberFields()
    {
        var row = ArtworkTableRow.FromArtwork(SavedArtwork());

        Assert.Equal("1998", row.YearText);
        Assert.Equal("12.5", row.HeightText);
        Assert.Equal("30", row.WidthText);
        Assert.Null(row.DepthText);
        Assert.True(row.Validate());
    }

    [Fact]
    public void TheThumbnailIsTheStarredImage()
    {
        var row = ArtworkTableRow.FromArtwork(SavedArtwork());

        Assert.Equal(Image('b'), row.Thumbnail);
    }

    [Fact]
    public void ReplacingTheImagesKeepsTheOnesThatFailedToUpload()
    {
        var row = ArtworkTableRow.FromDrop(
            new ArtworkId(1),
            new PlannedArtworkFromImages(new ArtworkName("Dawn"), [], NewSeriesName: null, [new BulkImageCandidate("a", "/P/Dawn.jpg"), new BulkImageCandidate("b", "/P/Dawn (2).jpg")]),
            []
        );
        row.Images[0].State = RowImageState.Added;
        row.Images[0].Image = Image('a');
        row.Images[1].State = RowImageState.Failed;

        row.ReplaceSavedImages([Image('c'), Image('a')], mainImageKey: null);

        Assert.Equal([Image('c'), Image('a')], row.SavedImages);
        Assert.Equal(Image('c'), row.Thumbnail);
        Assert.Equal([RowImageState.Added, RowImageState.Added, RowImageState.Failed], [.. row.Images.Select(image => image.State)]);
    }

    // the size columns keep 4 decimal places, so the field shows the size as it's saved
    [Fact]
    public void ASizeIsShownAndSavedAsTheDatabaseKeepsIt()
    {
        var row = Row();

        row.HeightText = "30.0";
        row.WidthText = "30.24453333";

        Assert.Equal("30", row.HeightText);
        Assert.Equal("30.2445", row.WidthText);
        Assert.Equal(30.2445m, row.Form.WidthCm);
    }

    [Fact]
    public void TextThatIsNotANumberStaysAsTyped()
    {
        var row = Row();

        row.HeightText = "12,5";

        Assert.Equal("12,5", row.HeightText);
    }
}
