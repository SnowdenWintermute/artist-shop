namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// The space inside a row around its content. Rows touch, so this is all the space between them and
// around the page's content. The sides are inside the row's CustomPageWidths.PageMaximumClass box, so
// they show however wide the screen is. Auto reproduces what pages had before rows held their padding: 56px
// above the first row, 96px below the last and 32px between rows; Medium sides, the default, the
// layout's 8px plus <main>'s 16px. A phone's notch, held sideways, adds to the sides. Tailwind only
// makes the classes it finds written out
public static class RowPaddingClasses
{
    public static string Vertical(RowPadding padding, RowPosition position) =>
        $"{Top(padding.Top, position)} {Bottom(padding.Bottom, position)}";

    private static string Top(RowVerticalPadding top, RowPosition position) =>
        top switch
        {
            RowVerticalPadding.Auto => position.IsFirst ? "pt-14" : "pt-4",
            RowVerticalPadding.None => "pt-0",
            RowVerticalPadding.Small => "pt-4",
            RowVerticalPadding.Medium => "pt-12",
            RowVerticalPadding.Large => "pt-24",
            _ => throw new InvalidOperationException($"{top} has no class."),
        };

    private static string Bottom(RowVerticalPadding bottom, RowPosition position) =>
        bottom switch
        {
            RowVerticalPadding.Auto => position.IsLast ? "pb-24" : "pb-4",
            RowVerticalPadding.None => "pb-0",
            RowVerticalPadding.Small => "pb-4",
            RowVerticalPadding.Medium => "pb-12",
            RowVerticalPadding.Large => "pb-24",
            _ => throw new InvalidOperationException($"{bottom} has no class."),
        };

    public static string Sides(RowSidePadding sides) =>
        sides switch
        {
            RowSidePadding.None => "pl-[env(safe-area-inset-left)] pr-[env(safe-area-inset-right)]",
            RowSidePadding.Small =>
                "pl-[calc(0.5rem_+_env(safe-area-inset-left))] pr-[calc(0.5rem_+_env(safe-area-inset-right))]",
            RowSidePadding.Medium =>
                "pl-[calc(1.5rem_+_env(safe-area-inset-left))] pr-[calc(1.5rem_+_env(safe-area-inset-right))]",
            RowSidePadding.Large =>
                "pl-[calc(4rem_+_env(safe-area-inset-left))] pr-[calc(4rem_+_env(safe-area-inset-right))]",
            _ => throw new InvalidOperationException($"{sides} has no class."),
        };

    // Sides' padding on each side, leaving out a notch
    public static int SidePixels(RowSidePadding sides) =>
        sides switch
        {
            RowSidePadding.None => 0,
            RowSidePadding.Small => 8,
            RowSidePadding.Medium => 24,
            RowSidePadding.Large => 64,
            _ => throw new InvalidOperationException($"{sides} has no width."),
        };
}
