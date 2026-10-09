using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Domain;

public class SiteColorsTests
{
    private static SiteColors Choosing(params (ColorRole Role, string Hex)[] choices) =>
        new(choices.ToDictionary(choice => choice.Role, choice => RgbColor.Parse(choice.Hex)));

    [Fact]
    public void AWebsiteThatChoseNothingHasThePlatformsColors()
    {
        Assert.All(ColorRoles.All, definition => Assert.Equal(definition.Platform, SiteColors.Default.Resolve(definition.Role)));
    }

    [Fact]
    public void ThePlatformsColorsAreEasyToRead()
    {
        Assert.Empty(SiteColors.Default.ContrastProblems());
    }

    // the platform's were chosen by hand, which derivation can't reproduce
    [Fact]
    public void ChoosingOnlyANonBaseRoleKeepsThePlatformsColorsForTheRest()
    {
        var colors = Choosing((ColorRole.Bar, "#123456"));

        Assert.Equal(RgbColor.Parse("#123456"), colors.Resolve(ColorRole.Bar));
        Assert.Equal(ColorRoles.For(ColorRole.InkFaded).Platform, colors.Resolve(ColorRole.InkFaded));
    }

    [Fact]
    public void ChoosingABaseRoleDerivesTheRestFromIt()
    {
        var colors = Choosing((ColorRole.Page, "#101010"), (ColorRole.Ink, "#f0f0f0"), (ColorRole.Accent, "#ffd000"));

        // a dark page gets panels a step lighter, rather than white
        Assert.Equal(RgbColor.Parse("#101010").Mix(RgbColor.Parse("#f0f0f0"), 0.08), colors.Resolve(ColorRole.Panel));
        // a light accent gets black text
        Assert.Equal(RgbColor.Black, colors.Resolve(ColorRole.OnAccent));
        Assert.Equal(RgbColor.Parse("#ffd000"), colors.Resolve(ColorRole.LinkHover));
        Assert.Equal(RgbColor.Parse("#f0f0f0"), colors.Resolve(ColorRole.Link));
        Assert.Empty(colors.ContrastProblems());
    }

    [Fact]
    public void ARoleChosenAlongsideABaseRoleKeepsItsChoice()
    {
        var colors = Choosing((ColorRole.Page, "#ffffff"), (ColorRole.Link, "#aa0000"));

        Assert.Equal(RgbColor.Parse("#aa0000"), colors.Resolve(ColorRole.Link));
    }

    [Fact]
    public void TextTooCloseToItsBackgroundIsAProblem()
    {
        var problem = Assert.Single(Choosing((ColorRole.Page, "#ffffff"), (ColorRole.Ink, "#dddddd"), (ColorRole.InkFaded, "#555555"), (ColorRole.InkUnavailable, "#777777"), (ColorRole.Link, "#000000")).ContrastProblems(),
            problem => problem.Foreground == ColorRole.Ink && problem.Background == ColorRole.Page);

        Assert.Equal(4.5, problem.Minimum);
        Assert.True(problem.Ratio < 1.5);
    }
}
