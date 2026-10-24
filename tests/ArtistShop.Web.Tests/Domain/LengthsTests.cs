using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Domain;

public class LengthsTests
{
    [Theory]
    [InlineData("10")]
    [InlineData("10.0625")]
    [InlineData("0.01")]
    [InlineData("3937.0078")]
    public void ALengthTypedInInchesShowsAgainAsTyped(string typed)
    {
        var inches = decimal.Parse(typed, System.Globalization.CultureInfo.InvariantCulture);

        var centimeters = Lengths.ToCentimeters(inches, LengthUnit.Inches, saved: null);

        Assert.Equal(inches, Lengths.ForEntry(centimeters, LengthUnit.Inches));
    }

    [Fact]
    public void ALengthSavedInCentimetresAndLeftAsShownInInchesKeepsItsCentimetres()
    {
        var shown = Lengths.ForEntry(30m, LengthUnit.Inches);

        Assert.Equal(11.811m, shown);
        Assert.Equal(30m, Lengths.ToCentimeters(shown, LengthUnit.Inches, saved: 30m));
        // what converting back would have saved instead
        Assert.Equal(29.9999m, Lengths.ToCentimeters(shown, LengthUnit.Inches, saved: null));
    }

    [Fact]
    public void AChangedLengthIsConverted()
    {
        Assert.Equal(30.48m, Lengths.ToCentimeters(12m, LengthUnit.Inches, saved: 30m));
        Assert.Equal(12.5m, Lengths.ToCentimeters(12.5m, LengthUnit.Centimeters, saved: 30m));
    }

    [Fact]
    public void TheLargestLengthInInchesFitsTheColumns()
    {
        var maximum = Lengths.Maximum(LengthUnit.Inches);

        Assert.True(Lengths.ToCentimeters(maximum, LengthUnit.Inches, saved: null) <= ArtistShopLimits.MaximumDimensionCm);
        Assert.Equal(maximum, Lengths.ForEntry(ArtistShopLimits.MaximumDimensionCm, LengthUnit.Inches));
    }

    [Theory]
    [InlineData(DimensionDisplay.Centimeters, "30 × 40 × 2.5 cm")]
    [InlineData(DimensionDisplay.Inches, "11.81 × 15.75 × 0.98 in")]
    [InlineData(DimensionDisplay.InchesThenCentimeters, "11.81 × 15.75 × 0.98 in (30 × 40 × 2.5 cm)")]
    public void DimensionsReadInTheChosenUnits(DimensionDisplay display, string expected)
    {
        var dimensions = new DimensionsCentimeters(new Dimensions(30m, 40m, 2.5m));

        Assert.Equal(expected, dimensions.Text(display));
    }
}
