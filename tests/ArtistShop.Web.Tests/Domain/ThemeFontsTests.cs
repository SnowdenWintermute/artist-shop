using System.Globalization;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Domain;

public sealed class ThemeFontsTests
{
    // so the platform's fonts look as they did before themes had fonts
    [Fact]
    public void AReferenceFontAtFullSizeKeepsItsOwnSize()
    {
        Assert.All(FontRoles.All, definition =>
        {
            Assert.Equal(1, definition.Scale(definition.ReferenceFont, 100), precision: 6);
            Assert.Equal(definition.SizeAdjust(definition.ReferenceFont, 100), Fonts.For(definition.ReferenceFont).RecordedXHeight.ToString("0.###", CultureInfo.InvariantCulture));
        });
    }

    // Sancreek's file states an x-height of 0.196 where its x is 0.640 tall; a browser divides the
    // adjust by the stated one, so the adjust is that over again
    [Fact]
    public void AFontThatStatesItsXHeightWronglyIsStillDrawnAtItsScale()
    {
        var heading = FontRoles.For(FontRole.Heading);
        var sancreek = Fonts.For(Font.Sancreek);

        var drawnAt = double.Parse(heading.SizeAdjust(Font.Sancreek, 100), CultureInfo.InvariantCulture) / sancreek.RecordedXHeight;

        Assert.Equal(heading.Scale(Font.Sancreek, 100), drawnAt, precision: 2);
    }

    // a font of capitals only keeps nearly its own size as a heading, where by lowercase letters it
    // would shrink to Josefin Slab's short ones
    [Fact]
    public void HeadingsAreSizedByTheirCapitals()
    {
        var expected = Fonts.For(Font.JosefinSlab).CapHeight / Fonts.For(Font.Bungee).CapHeight;

        Assert.Equal(expected, FontRoles.For(FontRole.Heading).Scale(Font.Bungee, 100), precision: 6);
        Assert.Equal(2 * expected, FontRoles.For(FontRole.Heading).Scale(Font.Bungee, 200), precision: 6);
    }

    [Fact]
    public void OnlyTheHeadingFontGoesPast150Percent()
    {
        Assert.True(FontRoles.For(FontRole.Heading).AllowsSize(200));
        Assert.False(FontRoles.For(FontRole.Text).AllowsSize(155));
        Assert.False(FontRoles.For(FontRole.Text).AllowsSize(102));
    }

    [Fact]
    public void TheTextFontCantBeHeadingOnly()
    {
        Assert.True(FontRoles.For(FontRole.Heading).Allows(Font.Pacifico));
        Assert.False(FontRoles.For(FontRole.Text).Allows(Font.Pacifico));
        Assert.True(FontRoles.For(FontRole.Text).Allows(Font.Bitter));
    }

    [Fact]
    public void EveryPresetsFontsSuitTheirRoles()
    {
        Assert.All(ThemePresets.All, preset => Assert.All(FontRoles.All, definition =>
        {
            var choice = preset.Theme.Fonts.For(definition.Role);
            Assert.True(definition.Allows(choice.Font) && definition.AllowsSize(choice.SizePercent));
        }));
    }
}
