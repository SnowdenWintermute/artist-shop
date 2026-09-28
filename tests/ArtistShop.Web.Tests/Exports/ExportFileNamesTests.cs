using ArtistShop.Web.Exports;

namespace ArtistShop.Web.Tests.Exports;

public sealed class ExportFileNamesTests
{
    [Theory]
    [InlineData("Sunset over the bay.jpg")]
    [InlineData("Café, midi.png")]
    [InlineData("Console.csv")]
    public void APortableName(string name) => Assert.True(ExportFileNames.IsPortable(name));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("..")]
    [InlineData("Night/Day.jpg")]
    [InlineData("What?.jpg")]
    [InlineData("Time: 3pm.jpg")]
    [InlineData("Ends with a dot.")]
    [InlineData("con.jpg")]
    [InlineData("LPT1")]
    [InlineData("tab\there.jpg")]
    public void NotAPortableName(string name) => Assert.False(ExportFileNames.IsPortable(name));

    // 255 bytes, not characters: "é" takes two
    [Fact]
    public void ANameOverTheByteLimitIsntPortable()
    {
        Assert.True(ExportFileNames.IsPortable(new string('a', 251) + ".jpg"));
        Assert.False(ExportFileNames.IsPortable(new string('é', 126) + ".jpg"));
    }
}
