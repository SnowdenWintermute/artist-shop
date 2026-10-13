namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Components.Publishing;
using ArtistShop.Web.Domain.Website;

// how wide an article's text runs on the page
public static class ArticleColumns
{
    public static string PageClass(ArticleWidth width) =>
        width switch
        {
            ArticleWidth.Narrow => $"mx-auto w-full {PostColumn.WidthClass}",
            ArticleWidth.Full => "w-full",
            _ => throw new InvalidOperationException($"{width} has no class."),
        };
}
