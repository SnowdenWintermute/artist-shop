using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Domain;

public class ThemeColorsTests
{
    private static ThemeColors Choosing(params (ColorRole Role, string Hex)[] choices) =>
        new(choices.ToDictionary(choice => choice.Role, choice => RgbColor.Parse(choice.Hex)));

    [Fact]
    public void EveryPresetIsEasyToRead()
    {
        Assert.All(ThemePresets.All, preset => Assert.Empty(preset.Theme.Colors.ContrastProblems()));
    }

    // Paper is the platform's look, so what admin pages have
    [Fact]
    public void PaperHasThePlatformsColors()
    {
        var paper = ThemePresets.Paper.Theme.Colors;

        Assert.Equal(RgbColor.Parse("#f5f5f5"), paper.Resolve(ColorRole.Page));
        Assert.Equal(RgbColor.Parse("#5f6878"), paper.Resolve(ColorRole.InkFaded));
        Assert.Equal(RgbColor.White, paper.Resolve(ColorRole.Panel));
        Assert.Equal(RgbColor.White, paper.Resolve(ColorRole.OnAccent));
        Assert.Equal(RgbColor.Black.WithOpacityPercent(40), paper.Resolve(ColorRole.Backdrop));
    }

    [Fact]
    public void AThemeChoosesEveryBaseRole()
    {
        Assert.Throws<ArgumentException>(() => Choosing((ColorRole.Page, "#ffffff"), (ColorRole.Ink, "#000000")));
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
        Assert.False(colors.ChoosesADerivableRole);
    }

    [Fact]
    public void ARoleChosenAlongsideTheBaseRolesKeepsItsChoice()
    {
        var colors = Choosing((ColorRole.Page, "#ffffff"), (ColorRole.Ink, "#000000"), (ColorRole.Accent, "#155dfc"), (ColorRole.Link, "#aa0000"));

        Assert.Equal(RgbColor.Parse("#aa0000"), colors.Resolve(ColorRole.Link));
        Assert.True(colors.ChoosesADerivableRole);
    }

    [Fact]
    public void TextTooCloseToItsBackgroundIsAProblem()
    {
        var colors = Choosing((ColorRole.Page, "#ffffff"), (ColorRole.Ink, "#dddddd"), (ColorRole.Accent, "#155dfc"));

        var problem = Assert.Single(colors.ContrastProblems(), problem => problem.Foreground == ColorRole.Ink && problem.Background == ColorRole.Page);

        Assert.Equal(4.5, problem.Minimum);
        Assert.True(problem.Ratio < 1.5);
    }

    [Fact]
    public void AThemeKeyComesBackFromItsQueryValue()
    {
        ThemeKey[] keys = [new ThemeKey.Preset(ThemePreset.Dark), new ThemeKey.Saved(new ThemeId(12))];

        Assert.All(keys, key => Assert.Equal(key, ThemeKey.FromQueryValue(key.QueryValue)));
        Assert.Null(ThemeKey.FromQueryValue("Sunrise"));
        Assert.Null(ThemeKey.FromQueryValue(null));
    }
}
