namespace ArtistShop.Web.Imports;

// What the Upload images and Add from images pages take from one drop
public static class BulkImageDrop
{
    // one drop at a time. Well past any work folder, and it keeps the name pre-check's
    // table-valued parameter and the report to a size worth rendering
    public const int MaximumFiles = 5_000;

    // a backstop only: the count above is the real limit, and 400 bytes is generous for an entry
    // of an id, a path, a size and a content type
    public const long MaximumMetadataBytes = MaximumFiles * 400L;

    // the dropped folder and one level of folders inside it. Deeper than that is someone's thumbnails
    public const int MaximumDirectoryDepth = 2;

    // "/Seascapes/1990s/sunset.jpg" is in Seascapes, then 1990s; a loose file is in none
    public static string[] FoldersOf(string path) => path.Trim('/').Split('/')[..^1];
}
