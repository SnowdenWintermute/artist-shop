namespace ArtistShop.Web.Components.CustomPages;

// how wide a custom page's rows are drawn, and the screens the row editor's preview draws a row on,
// so its container queries switch where a visitor's will
public static class CustomPageWidths
{
    // a row's box, its side padding inside, so Medium sides leave 1168px of content as pages had
    // before rows held their padding
    public const int PageMaximumPixels = 1216;

    public const string PageMaximumClass = "max-w-[1216px]";

    // the narrowest screen that shows a row at its widest
    public const int DesktopPixels = PageMaximumPixels;

    // a common phone's screen
    public const int PhonePixels = 390;
}
