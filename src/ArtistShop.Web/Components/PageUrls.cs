using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

namespace ArtistShop.Web.Components;

// The one place each page's address is spelled, other than its own @page line. The artwork page
// has query parameters too, so it has its own, ArtworkPageQuery
public static class PageUrls
{
    public static string Series(SeriesSlug slug) => $"/series/{slug.Value}";

    public static string EditArtwork(ArtworkId id) => $"/admin/catalog/artworks/{id.Value}/edit";

    public const string PostList = "/admin/posts";

    public const string NewPost = "/admin/posts/new";

    public static string EditPost(PostId id) => $"/admin/posts/{id.Value}/edit";

    public static string Post(PostSlug slug) => $"/posts/{slug.Value}";
}
