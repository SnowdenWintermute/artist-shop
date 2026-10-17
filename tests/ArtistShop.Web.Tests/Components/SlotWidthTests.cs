using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Components;

public class SlotWidthTests
{
    [Fact]
    public void AWholeRowIsThePageWidthUpToTheImage()
    {
        Assert.Equal("min(800px, calc(min(100vw, 1200px) - 32px))", new SlotWidth(RowWidths.Whole, 0).Sizes(800));
    }

    // never narrower than the slot's minimum, which holds a narrow share wider than its part of the line
    [Fact]
    public void ASlotOnALineWithOthersIsItsShareOfThePage()
    {
        Assert.Equal(
            "(min-width: 960px) min(2400px, max(288px, calc((min(100vw, 1200px) - 96px) * 2 / 4))), min(2400px, calc(min(100vw, 1200px) - 32px))",
            new SlotWidth(RowWidths.QuarterHalfQuarter, 1).Sizes(2400)
        );
    }
}
