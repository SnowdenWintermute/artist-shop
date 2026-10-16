namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// Tailwind only makes the classes it finds written out, so each split is spelled here. Slots share
// what's left after the gaps by their growth, and one narrower than its minimum wraps onto a line
// of its own, so a row stacks on a phone
public static class RowClasses
{
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

    public static string Alignment(RowAlignment alignment) =>
        alignment switch
        {
            RowAlignment.Start => "items-start",
            RowAlignment.Center => "items-center",
            RowAlignment.End => "items-end",
            _ => throw new InvalidOperationException($"{alignment} has no class."),
        };
}
