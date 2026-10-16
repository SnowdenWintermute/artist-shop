namespace ArtistShop.Web.Components.CustomPages;

using System.Globalization;
using ArtistShop.Web.Domain.Website;

// Where a slot sits in its row, so an image in it loads a size near the slot's width rather than the
// page's. The pixels match the page's p-4 and PageRowView's gap-8 and min-w-[min(100%,18rem)]
public sealed record SlotWidth(RowWidths Widths, int Index)
{
    private const int PagePaddingPixels = 32;
    private const int GapPixels = 32;
    private const int SlotMinimumPixels = 288;

    // Once the screen fits every slot at its minimum, they share one line; narrower, a slot may take a
    // line of its own, so the page's width is what it can reach. Never more than the image's own width
    public string Sizes(int imagePixels)
    {
        var shares = RowClasses.SlotShares(Widths);
        var image = $"{imagePixels.ToString(CultureInfo.InvariantCulture)}px";
        var wholeLine = $"min({image}, calc(min(100vw, {Pixels(CustomPageWidths.DesktopPixels)}) - {Pixels(PagePaddingPixels)}))";

        if (shares.Count == 1)
        {
            return wholeLine;
        }

        var oneLineFrom = PagePaddingPixels + shares.Count * SlotMinimumPixels + (shares.Count - 1) * GapPixels;
        var gaps = PagePaddingPixels + (shares.Count - 1) * GapPixels;
        var share = $"{shares[Index].ToString(CultureInfo.InvariantCulture)} / {shares.Sum().ToString(CultureInfo.InvariantCulture)}";

        return $"(min-width: {Pixels(oneLineFrom)}) min({image}, calc((min(100vw, {Pixels(CustomPageWidths.DesktopPixels)}) - {Pixels(gaps)}) * {share})), {wholeLine}";
    }

    private static string Pixels(int pixels) => $"{pixels.ToString(CultureInfo.InvariantCulture)}px";
}
