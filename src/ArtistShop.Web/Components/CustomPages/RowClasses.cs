namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// Tailwind only makes the classes it finds written out, so each split is spelled here. Slots share
// what's left after the gaps by their growth, and one narrower than its minimum wraps onto a line
// of its own, so a row stacks on a phone
public static class RowClasses
{
    public static string GapClass(RowGap gap) =>
        gap switch
        {
            RowGap.None => "gap-0",
            RowGap.Small => "gap-4",
            RowGap.Medium => "gap-8",
            RowGap.Large => "gap-16",
            _ => throw new InvalidOperationException($"{gap} has no class."),
        };

    public static int GapPixels(RowGap gap) =>
        gap switch
        {
            RowGap.None => 0,
            RowGap.Small => 16,
            RowGap.Medium => 32,
            RowGap.Large => 64,
            _ => throw new InvalidOperationException($"{gap} has no width."),
        };

    public const string SlotMinimumWidthClass = "min-w-[min(100%,18rem)]";

    public const int SlotMinimumPixels = 288;

    // each slot's share of the row, left to right
    public static IReadOnlyList<int> SlotShares(RowWidths widths) =>
        widths switch
        {
            RowWidths.Whole => [1],
            RowWidths.Halves => [1, 1],
            RowWidths.ThirdThenTwoThirds => [1, 2],
            RowWidths.TwoThirdsThenThird => [2, 1],
            RowWidths.Thirds => [1, 1, 1],
            RowWidths.QuarterHalfQuarter => [1, 2, 1],
            _ => throw new InvalidOperationException($"{widths} has no slot shares."),
        };

    public static IReadOnlyList<string> SlotGrowth(RowWidths widths) =>
        [
            .. SlotShares(widths)
                .Select(share =>
                    share switch
                    {
                        1 => "grow",
                        2 => "grow-2",
                        _ => throw new InvalidOperationException($"A share of {share} has no class."),
                    }
                ),
        ];

    // a row that fills the screen, drawn where nothing else sets its height. On a public page that's
    // the screen; the row editor's preview sets --screen-height to its own screen
    public const string ScreenHeightClass = "min-h-[var(--screen-height,100svh)]";

    // Where each slot's stack sits in the slot. Slots stretch to the row's height, so an image set to
    // Fill can take a slot as tall as the tallest beside it
    public static string StackAlignment(RowAlignment alignment) =>
        alignment switch
        {
            RowAlignment.Start => "justify-start",
            RowAlignment.Center => "justify-center",
            RowAlignment.End => "justify-end",
            _ => throw new InvalidOperationException($"{alignment} has no class."),
        };

}
