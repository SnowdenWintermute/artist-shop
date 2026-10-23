using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Domain;

public class CustomComponentSettingsJsonTests
{
    [Fact]
    public void AHeadingReadsBackAsWritten()
    {
        var heading = new HeadingSettings("About me", TextSize.Size160, FontRole.Text, Bold: false, HeadingAlignment.Right);
        var (json, version) = CustomComponentSettingsJson.Write(heading);

        Assert.Equal(heading, CustomComponentSettingsJson.Read(CustomComponentType.Heading, version, json));
    }

    // version 1, when a heading's level gave its size and font, and every level was bold
    [Theory]
    [InlineData("One", TextSize.Size36, FontRole.Heading)]
    [InlineData("Two", TextSize.Size24, FontRole.Heading)]
    [InlineData("Three", TextSize.Size18, FontRole.Text)]
    public void ALeveledHeadingKeepsItsLook(string level, TextSize size, FontRole font)
    {
        var json = $$"""{"text": "About me", "level": "{{level}}", "alignment": "Center"}""";

        Assert.Equal(
            new HeadingSettings("About me", size, font, Bold: true, HeadingAlignment.Center),
            CustomComponentSettingsJson.Read(CustomComponentType.Heading, 1, json)
        );
    }
}
