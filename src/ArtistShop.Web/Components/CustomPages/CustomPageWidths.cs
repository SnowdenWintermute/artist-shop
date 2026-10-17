namespace ArtistShop.Web.Components.CustomPages;

// how wide a custom page is drawn, which the row editor's preview draws a row at so its container
// queries switch where a visitor's will
public static class CustomPageWidths
{
    public const string PaddingClass = "p-4";

    // PaddingClass on each side
    public const int PaddingPixels = 16;

    // the page's <main>, which these widths include the padding of
    public const string MainClass = $"mx-auto flex max-w-[1200px] flex-col gap-8 {PaddingClass}";

    public const int DesktopPixels = 1200;

    // a common phone's screen
    public const int PhonePixels = 390;
}
