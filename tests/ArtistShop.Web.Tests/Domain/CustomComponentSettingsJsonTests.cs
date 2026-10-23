using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Domain;

public class CustomComponentSettingsJsonTests
{
    [Fact]
    public void AHeadingReadsBackAsWritten()
    {
        var heading = new HeadingSettings("About me", TextSize.Size160, FontRole.Text, Bold: false, ComponentAlignment.Right, LinkTarget: null);
        var (json, version) = CustomComponentSettingsJson.Write(heading, new ComponentFrame.Off());

        Assert.Equal(heading, CustomComponentSettingsJson.Read(CustomComponentType.Heading, version, json));
    }

    [Theory]
    [MemberData(nameof(LinkTargets))]
    public void ALinkedHeadingReadsBackAsWritten(LinkTarget target)
    {
        var heading = HeadingSettings.New with { LinkTarget = target };
        var (json, version) = CustomComponentSettingsJson.Write(heading, new ComponentFrame.Off());

        Assert.Equal(heading, CustomComponentSettingsJson.Read(CustomComponentType.Heading, version, json));
    }

    public static TheoryData<LinkTarget> LinkTargets =>
    [
        new LinkTarget.Page(new CustomPageId(3)),
        new LinkTarget.Work(new WorkId(4)),
        new LinkTarget.Collection(new CollectionId(5)),
        new LinkTarget.Post(new PostId(6)),
        new LinkTarget.Address("https://example.com/shop"),
    ];

    [Fact]
    public void AButtonReadsBackAsWritten()
    {
        var button = new ButtonSettings("Shop", new LinkTarget.Post(new PostId(2)), ComponentAlignment.Right, ButtonStyle.Outline,
            Size: 1, ButtonColor.Normal, FrameBorderWidth.None, Corners: null);
        var (json, version) = CustomComponentSettingsJson.Write(button, new ComponentFrame.Theme());

        Assert.Equal(button, CustomComponentSettingsJson.Read(CustomComponentType.Button, version, json));
    }

    [Fact]
    public void ACustomFrameReadsBackAsWritten()
    {
        var frame = new ComponentFrame.Custom(RgbColor.Parse("#11223366"), null, FrameBorderWidth.Medium, null, FramePadding.Large);
        var (json, _) = CustomComponentSettingsJson.Write(new PostListSettings(), frame);

        Assert.Equal(frame, CustomComponentSettingsJson.ReadFrame(json));
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
            new HeadingSettings("About me", size, font, Bold: true, ComponentAlignment.Center, LinkTarget: null),
            CustomComponentSettingsJson.Read(CustomComponentType.Heading, 1, json)
        );
    }
}
