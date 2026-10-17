namespace ArtistShop.Web.Components.CustomPages;

using System.Globalization;
using ArtistShop.Web.Domain.Website;

// Where a slot sits in its row: its growth class, and the width an image in it loads a size for, near the
// slot's rather than the page's
public sealed record SlotWidth(RowWidths Widths, RowSidePadding Sides, int Index)
{
    public string GrowthClass => RowClasses.SlotGrowth(Widths)[Index];

    // Once the screen fits every slot at its minimum, they share one line, where a narrow share is held
    // at the minimum; narrower, a slot may take a line of its own, so the page's width is what it can
    // reach. Never more than the image's own width
    public string Sizes(int imagePixels)
    {
        var shares = RowClasses.SlotShares(Widths);
        var image = Pixels(imagePixels);
        var padding = 2 * RowPaddingClasses.SidePixels(Sides);
        var content = $"calc(min(100vw, {Pixels(CustomPageWidths.PageMaximumPixels)}) - {Pixels(padding)})";
        var wholeLine = $"min({image}, {content})";

        if (shares.Count == 1)
        {
            return wholeLine;
        }

        var gaps = (shares.Count - 1) * RowClasses.GapPixels;
        var oneLineFrom = padding + gaps + shares.Count * RowClasses.SlotMinimumPixels;
        var share = $"{shares[Index].ToString(CultureInfo.InvariantCulture)} / {shares.Sum().ToString(CultureInfo.InvariantCulture)}";
        var slot = $"max({Pixels(RowClasses.SlotMinimumPixels)}, calc(({content} - {Pixels(gaps)}) * {share}))";

        return $"(min-width: {Pixels(oneLineFrom)}) min({image}, {slot}), {wholeLine}";
    }

    private static string Pixels(int pixels) => $"{pixels.ToString(CultureInfo.InvariantCulture)}px";
}
