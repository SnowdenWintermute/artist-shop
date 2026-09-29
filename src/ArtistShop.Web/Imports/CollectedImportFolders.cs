using ArtistShop.Web.Components.Forms.FileUpload;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Imports;

// a file's text, as a StreamReader reads it; given the most it may be, which the file is within
public delegate Task<string> ReadCollectedText(CollectedFile file, long maximumBytes);

// A chosen folder's files read into what the imports plan with. Pure but for readText, so the tests
// read a download the way the import pages do
public static class CollectedImportFolders
{
    // a post's text, which is small; its images are separate files
    public const long MaximumPostFileBytes = 5L * Units.BytesPerMebibyte;

    // the catalog's CSV files and images.csv, which grow with the number of artworks
    public const long MaximumListBytes = 50L * Units.BytesPerMebibyte;

    // Every post.json in a folder isPostFolder accepts, with the other files of its folder by name
    public static async Task<List<PostImportFolder>> PostsAsync(
        IReadOnlyList<(string Path, CollectedFile File)> files,
        Func<string, bool> isPostFolder,
        ReadCollectedText readText
    )
    {
        var byFolder = files.ToLookup(file => CollectedPaths.FolderOf(file.Path));
        var posts = new List<PostImportFolder>();

        foreach (var (path, postFile) in files.Where(file => CollectedPaths.NameOf(file.Path) == PostExportJson.FileName))
        {
            var folder = CollectedPaths.FolderOf(path);

            if (!isPostFolder(folder))
            {
                continue;
            }

            posts.Add(
                new PostImportFolder(
                    CollectedPaths.NameOf(folder),
                    await TextAsync(postFile, MaximumPostFileBytes, readText),
                    byFolder[folder]
                        .Where(file => file.File.Id != postFile.Id)
                        .ToDictionary(file => CollectedPaths.NameOf(file.Path), file => file.File.Id)
                )
            );
        }

        return posts;
    }

    // The folder website.json is in is the download's own, the shallowest in case the folder holds
    // another download inside it; everything else is found by its path inside it. With no
    // website.json nothing else is read, and the review says what's wrong
    public static async Task<WebsiteImportFolder> WebsiteAsync(IReadOnlyList<CollectedFile> files, ReadCollectedText readText)
    {
        var manifest = files
            .Where(file => CollectedPaths.NameOf(file.Path) == WebsiteExportArchive.ManifestFileName)
            .MinBy(file => file.Path.Trim('/').Count(character => character == '/'));

        if (manifest is null)
        {
            return new WebsiteImportFolder(null, null, null, new Dictionary<string, string>(), null, new Dictionary<string, string>(), []);
        }

        var root = CollectedPaths.FolderOf(manifest.Path);
        var byPath = new Dictionary<string, CollectedFile>();

        foreach (var file in files)
        {
            if (CollectedPaths.Inside(root, file.Path) is { } path)
            {
                byPath[path] = file;
            }
        }

        async Task<string?> TextAtAsync(string path, long maximumBytes) =>
            byPath.TryGetValue(path, out var file) ? await TextAsync(file, maximumBytes, readText) : null;

        const string catalog = WebsiteExportArchive.CatalogFolder;
        const string artworkFolder = $"{catalog}/{CatalogExportArchive.ArtworksFolder}";
        const string images = WebsiteExportArchive.ImagesFolder;
        var artworkCsvs = new Dictionary<string, string>();
        var imageFileIds = new Dictionary<string, string>();

        foreach (var (path, file) in byPath)
        {
            if (CollectedPaths.FolderOf(path) == artworkFolder)
            {
                artworkCsvs[CollectedPaths.NameOf(path)] = await TextAsync(file, MaximumListBytes, readText);
            }

            if (CollectedPaths.Inside(images, path) is { } imagePath)
            {
                imageFileIds[imagePath] = file.Id;
            }
        }

        return new WebsiteImportFolder(
            await TextAtAsync(WebsiteExportArchive.ManifestFileName, MaximumPostFileBytes),
            await TextAtAsync($"{catalog}/{CatalogExportArchive.ArtworkTypesFileName}", MaximumListBytes),
            await TextAtAsync($"{catalog}/{CatalogExportArchive.VocabulariesFileName}", MaximumListBytes),
            artworkCsvs,
            await TextAtAsync($"{images}/{ImageExportArchive.ImageListFileName}", MaximumListBytes),
            imageFileIds,
            // a post's folder sits directly in posts/
            await PostsAsync(
                [.. byPath.Select(entry => (entry.Key, entry.Value))],
                folder => CollectedPaths.FolderOf(folder) == WebsiteExportArchive.PostsFolder,
                readText
            )
        );
    }

    // one over the limit reads as empty, which the review reports as unreadable
    private static async Task<string> TextAsync(CollectedFile file, long maximumBytes, ReadCollectedText readText) =>
        file.Size > maximumBytes ? "" : await readText(file, maximumBytes);
}
