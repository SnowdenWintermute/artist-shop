namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// how wide a custom page's rows are drawn, and the screens the editors' previews draw on, so media and
// container queries switch where a visitor's will
public static class CustomPageWidths
{
    // a page-width row's box, its side padding inside, so Medium sides leave 1168px of content as
    // pages had before rows held their padding
    public const int PageMaximumPixels = 1216;

    // an edge-to-edge row has no maximum
    public static string MaximumClass(RowContentWidth width) =>
        width switch
        {
            RowContentWidth.Page => "max-w-[1216px]",
            RowContentWidth.EdgeToEdge => "",
            _ => throw new InvalidOperationException($"{width} has no class."),
        };

    // a common laptop's screen, wider than a page-width row so an edge-to-edge one looks different
    public const int DesktopPixels = 1440;

    // a common phone's screen
    public const int PhonePixels = 390;
}
