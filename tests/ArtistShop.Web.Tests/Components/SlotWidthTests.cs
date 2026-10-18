using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Tests.Components;

public class SlotWidthTests
{
    private static PageRow Row(
        RowWidths widths,
        RowContentWidth contentWidth = RowContentWidth.Page,
        RowSidePadding sides = RowSidePadding.Medium,
        RowGap gap = RowGap.Medium
    ) => new(new PageRowId(1), widths, RowAlignment.Start, RowHeight.Natural, contentWidth, RowPadding.New with { Sides = sides }, gap, null, new RowBackground.None(), []);

    [Fact]
    public void AWholeRowIsThePageWidthUpToTheImage()
    {
        Assert.Equal("min(800px, calc(min(100vw, 1216px) - 48px))", new SlotWidth(Row(RowWidths.Whole), 0).Sizes(800));
    }

    // never narrower than the slot's minimum, which holds a narrow share wider than its part of the line
    [Fact]
    public void ASlotOnALineWithOthersIsItsShareOfThePage()
    {
        Assert.Equal(
            "(min-width: 976px) min(2400px, max(288px, calc((calc(min(100vw, 1216px) - 48px) - 64px) * 2 / 4))), min(2400px, calc(min(100vw, 1216px) - 48px))",
            new SlotWidth(Row(RowWidths.QuarterHalfQuarter), 1).Sizes(2400)
        );
    }

    [Fact]
    public void ARowWithoutSidePaddingIsTheScreenWidth()
    {
        Assert.Equal(
            "min(800px, calc(min(100vw, 1216px) - 0px))",
            new SlotWidth(Row(RowWidths.Whole, sides: RowSidePadding.None), 0).Sizes(800)
        );
    }

    [Fact]
    public void AnEdgeToEdgeRowIsTheScreenLessItsSides()
    {
        Assert.Equal(
            "min(2400px, calc(100vw - 48px))",
            new SlotWidth(Row(RowWidths.Whole, RowContentWidth.EdgeToEdge), 0).Sizes(2400)
        );
    }

    [Fact]
    public void SlotsWithoutAGapShareTheWholeRow()
    {
        Assert.Equal(
            "(min-width: 576px) min(2400px, max(288px, calc((calc(100vw - 0px) - 0px) * 1 / 2))), min(2400px, calc(100vw - 0px))",
            new SlotWidth(Row(RowWidths.Halves, RowContentWidth.EdgeToEdge, RowSidePadding.None, RowGap.None), 0).Sizes(2400)
        );
    }
}
