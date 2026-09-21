namespace ArtistShop.Web.Components.Pages.Catalog;

// The artwork page's whole address: which series the visitor is walking through, and which of the
// artwork's images to show. Everything that links to the page builds it here, and the page reads
// it back through the same names
public static class ArtworkPageQuery
{
    public const string SeriesKey = "series";
    public const string ImageKey = "image";

    // the address counts images from one, the way a person reads "image 2"; everything inside
    // counts from zero
    public static string Url(string artworkSlug, string? seriesSlug, int? imageNumber)
    {
        List<string> parts = [];

        if (seriesSlug is not null)
        {
            parts.Add($"{SeriesKey}={seriesSlug}");
        }

        if (imageNumber is int number)
        {
            parts.Add($"{ImageKey}={number}");
        }

        return parts.Count == 0
            ? $"/artworks/{artworkSlug}"
            : $"/artworks/{artworkSlug}?{string.Join("&", parts)}";
    }
}
