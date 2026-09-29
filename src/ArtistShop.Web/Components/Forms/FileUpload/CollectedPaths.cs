namespace ArtistShop.Web.Components.Forms.FileUpload;

// A CollectedFile's path read as folders. A drop's leading slash makes no difference
public static class CollectedPaths
{
    // "site-posts/spring-notes/post.json" is in "site-posts/spring-notes"; a file on its own is in ""
    public static string FolderOf(string path)
    {
        var trimmed = path.Trim('/');
        var slash = trimmed.LastIndexOf('/');

        return slash < 0 ? "" : trimmed[..slash];
    }

    public static string NameOf(string path) => path.Trim('/').Split('/')[^1];

    // the path inside folder, or null for a file that isn't in it
    public static string? Inside(string folder, string path)
    {
        var trimmed = path.Trim('/');

        return folder.Length == 0 ? trimmed
            : trimmed.StartsWith($"{folder}/", StringComparison.Ordinal) ? trimmed[(folder.Length + 1)..]
            : null;
    }
}
