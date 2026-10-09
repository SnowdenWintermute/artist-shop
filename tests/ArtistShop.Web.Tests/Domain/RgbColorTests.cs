using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Domain;

public class RgbColorTests
{
    [Theory]
    [InlineData("#0a1b2c")]
    [InlineData("#0a1b2c66")]
    public void HexReadsBackAsItWasWritten(string hex)
    {
        Assert.Equal(hex, RgbColor.Parse(hex).Hex);
    }

    [Theory]
    [InlineData("0a1b2c")]
    [InlineData("#0a1b2")]
    [InlineData("#0a1b2g")]
    [InlineData(null)]
    public void ANonHexColorIsRefused(string? hex)
    {
        Assert.False(RgbColor.TryParse(hex, out _));
    }

    // the slider's whole percentages survive the trip through a byte
    [Fact]
    public void EveryOpacityPercentComesBack()
    {
        Assert.All(Enumerable.Range(0, 101), percent => Assert.Equal(percent, RgbColor.Black.WithOpacityPercent(percent).OpacityPercent));
    }

    [Fact]
    public void BlackOnWhiteIsTheMostContrastThereIs()
    {
        Assert.Equal(21, RgbColor.Contrast(RgbColor.Black, RgbColor.White), 3);
        Assert.Equal(1, RgbColor.Contrast(RgbColor.White, RgbColor.White), 3);
    }
}
