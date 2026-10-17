using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Components;

public class SlotWidthTests
{
    [Fact]
    public void AWholeRowIsThePageWidthUpToTheImage()
    {
        Assert.Equal("min(800px, calc(min(100vw, 1216px) - 48px))", new SlotWidth(RowWidths.Whole, RowSidePadding.Medium, 0).Sizes(800));
    }

    // never narrower than the slot's minimum, which holds a narrow share wider than its part of the line
    [Fact]
    public void ASlotOnALineWithOthersIsItsShareOfThePage()
    {
        Assert.Equal(
            "(min-width: 976px) min(2400px, max(288px, calc((calc(min(100vw, 1216px) - 48px) - 64px) * 2 / 4))), min(2400px, calc(min(100vw, 1216px) - 48px))",
            new SlotWidth(RowWidths.QuarterHalfQuarter, RowSidePadding.Medium, 1).Sizes(2400)
        );
    }

    [Fact]
    public void ARowWithoutSidePaddingIsTheScreenWidth()
    {
        Assert.Equal("min(800px, calc(min(100vw, 1216px) - 0px))", new SlotWidth(RowWidths.Whole, RowSidePadding.None, 0).Sizes(800));
    }
}
