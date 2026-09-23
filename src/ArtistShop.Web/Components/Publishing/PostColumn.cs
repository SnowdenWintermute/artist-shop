namespace ArtistShop.Web.Components.Publishing;

// How wide a post's text is, on the post page and in the editor's writing area alike, so lines
// break and wrapped images sit in the same places in both. content-box, so the width is the text's
// own and padding around it never narrows it
public static class PostColumn
{
    public const string WidthClass = "box-content max-w-[42rem]";
}
