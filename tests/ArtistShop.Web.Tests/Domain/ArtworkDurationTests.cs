using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Domain;

public class ArtworkDurationTests
{
    [Theory]
    [InlineData("5:00", 300)]
    [InlineData("0:45", 45)]
    [InlineData("1:02:03", 3723)]
    public void ReadsBothSpellings(string text, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ArtworkDuration.TryParse(text));
    }

    [Theory]
    // a minute or a second past its limit belongs in the part above it
    [InlineData("5:60")]
    [InlineData("1:60:00")]
    [InlineData("0:00")]
    [InlineData("-1:00")]
    [InlineData("90")]
    [InlineData("1:2:3:4")]
    [InlineData("five minutes")]
    public void RefusesWhatIsNotADuration(string text)
    {
        Assert.Null(ArtworkDuration.TryParse(text));
    }

    [Theory]
    [InlineData(300, "5:00")]
    [InlineData(45, "0:45")]
    [InlineData(3723, "1:02:03")]
    public void WritesWhatItCanReadBack(int seconds, string expectedText)
    {
        var duration = TimeSpan.FromSeconds(seconds);

        Assert.Equal(expectedText, ArtworkDuration.ToText(duration));
        Assert.Equal(duration, ArtworkDuration.TryParse(ArtworkDuration.ToText(duration)));
    }
}
