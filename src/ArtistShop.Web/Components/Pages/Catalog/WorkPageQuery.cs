namespace ArtistShop.Web.Components.Pages.Catalog;

// The work page's whole address: which collection the visitor is walking through, and which of the
// work's images to show. Everything that links to the page builds it here, and the page reads
// it back through the same names
public static class WorkPageQuery
{
    public const string CollectionKey = "collection";
    public const string ImageKey = "image";

    // the address counts images from one, the way a person reads "image 2"; everything inside
    // counts from zero
    public static string Url(string workSlug, string? collectionSlug, int? imageNumber)
    {
        List<string> parts = [];

        if (collectionSlug is not null)
        {
            parts.Add($"{CollectionKey}={collectionSlug}");
        }

        if (imageNumber is int number)
        {
            parts.Add($"{ImageKey}={number}");
        }

        return parts.Count == 0
            ? $"/works/{workSlug}"
            : $"/works/{workSlug}?{string.Join("&", parts)}";
    }

    // for code holding an image's index, which counts from zero
    public static string ImageUrl(string workSlug, string? collectionSlug, int imageIndex) =>
        Url(workSlug, collectionSlug, imageIndex + 1);
}
