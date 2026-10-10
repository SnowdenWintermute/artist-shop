using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Domain;

public sealed class FontsTests
{
    [Fact]
    public void InOrderPutsThePlacedFontsFirstAndTheRestAfterInListOrder()
    {
        var ordered = Fonts.InOrder([Font.Pacifico, Font.Roboto, Font.Pacifico]).Select(definition => definition.Font).ToList();

        Assert.Equal([Font.Pacifico, Font.Roboto], ordered[..2]);
        Assert.Equal(Fonts.All.Select(definition => definition.Font).Where(font => font is not (Font.Pacifico or Font.Roboto)), ordered[2..]);
    }

    [Fact]
    public void AFontWithoutItsOwnBoldAndItalicIsHeadingOnly()
    {
        Assert.False(Fonts.For(Font.Roboto).IsHeadingOnly);
        Assert.True(Fonts.For(Font.Pacifico).IsHeadingOnly);
        // bold and italic, but no bold italic
        Assert.True(Fonts.For(Font.OldStandardTT).IsHeadingOnly);
    }

    [Fact]
    public void EveryFontIsListedOnce()
    {
        Assert.Equal(Enum.GetValues<Font>().Order(), Fonts.All.Select(definition => definition.Font).Order());
    }
}
