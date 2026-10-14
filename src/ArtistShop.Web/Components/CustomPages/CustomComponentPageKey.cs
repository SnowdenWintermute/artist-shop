namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// the query parameter a paged component keeps its page number in, its own so two on one page page separately
public static class CustomComponentPageKey
{
    public static string For(CustomComponentId id) => $"page-{id.Value}";
}
