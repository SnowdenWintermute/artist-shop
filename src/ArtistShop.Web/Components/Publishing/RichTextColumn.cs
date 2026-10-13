namespace ArtistShop.Web.Components.Publishing;

// How wide the text is, on the page and in the editor's writing area alike, so lines
// break and wrapped images sit in the same places in both. content-box, so the width is the text's
// own and padding around it never narrows it
public static class RichTextColumn
{
    public const string WidthClass = "box-content max-w-[42rem]";

    // that column, centred in a wider space
    public const string CenteredClass = $"mx-auto w-full {WidthClass}";
}
