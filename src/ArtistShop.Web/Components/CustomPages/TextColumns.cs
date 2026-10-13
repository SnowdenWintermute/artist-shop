namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Components.Publishing;
using ArtistShop.Web.Domain.Website;

// how wide a text component's text runs on the page
public static class TextColumns
{
    public static string PageClass(TextWidth width) =>
        width switch
        {
            TextWidth.Narrow => RichTextColumn.CenteredClass,
            TextWidth.Full => "w-full",
            _ => throw new InvalidOperationException($"{width} has no class."),
        };
}
