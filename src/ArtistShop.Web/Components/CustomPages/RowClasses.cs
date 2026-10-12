namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// Tailwind only makes the classes it finds written out, so each split is spelled here. Slots share
// what's left after the gaps by their growth, and one narrower than its minimum wraps onto a line
// of its own, so a row stacks on a phone
public static class RowClasses
{
    public static IReadOnlyList<string> SlotGrowth(RowWidths widths) =>
        widths switch
        {
            RowWidths.Whole => ["grow"],
            RowWidths.Halves => ["grow", "grow"],
            RowWidths.ThirdThenTwoThirds => ["grow", "grow-2"],
            RowWidths.TwoThirdsThenThird => ["grow-2", "grow"],
            RowWidths.Thirds => ["grow", "grow", "grow"],
            RowWidths.QuarterHalfQuarter => ["grow", "grow-2", "grow"],
            _ => throw new InvalidOperationException($"{widths} has no slot classes."),
        };

    public static string Alignment(RowAlignment alignment) =>
        alignment switch
        {
            RowAlignment.Start => "items-start",
            RowAlignment.Center => "items-center",
            RowAlignment.End => "items-end",
            _ => throw new InvalidOperationException($"{alignment} has no class."),
        };
}
