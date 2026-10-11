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

    // the catalog's CSV files and images.csv, which grow with the number of works
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
        if (DownloadFiles(files, WebsiteExportArchive.ManifestFileName) is not { } byPath)
        {
            return new WebsiteImportFolder(null, null, null, new Dictionary<string, string>(), null, new Dictionary<string, string>(), [], LookImportFolder.Empty);
        }

        async Task<string?> TextAtAsync(string path, long maximumBytes) =>
            byPath.TryGetValue(path, out var file) ? await TextAsync(file, maximumBytes, readText) : null;

        const string catalog = WebsiteExportArchive.CatalogFolder;
        const string workFolder = $"{catalog}/{CatalogExportArchive.WorksFolder}";
        const string images = WebsiteExportArchive.ImagesFolder;
        var workCsvs = new Dictionary<string, string>();
        var imageFileIds = new Dictionary<string, string>();
        var lookFiles = new Dictionary<string, CollectedFile>();

        foreach (var (path, file) in byPath)
        {
            if (CollectedPaths.FolderOf(path) == workFolder)
            {
                workCsvs[CollectedPaths.NameOf(path)] = await TextAsync(file, MaximumListBytes, readText);
            }

            if (CollectedPaths.Inside(images, path) is { } imagePath)
            {
                imageFileIds[imagePath] = file.Id;
            }

            if (CollectedPaths.Inside(WebsiteExportArchive.LookFolder, path) is { } lookPath)
            {
                lookFiles[lookPath] = file;
            }
        }

        return new WebsiteImportFolder(
            await TextAtAsync(WebsiteExportArchive.ManifestFileName, MaximumPostFileBytes),
            await TextAtAsync($"{catalog}/{CatalogExportArchive.WorkTypesFileName}", MaximumListBytes),
            await TextAtAsync($"{catalog}/{CatalogExportArchive.VocabulariesFileName}", MaximumListBytes),
            workCsvs,
            await TextAtAsync($"{images}/{ImageExportArchive.ImageListFileName}", MaximumListBytes),
            imageFileIds,
            // a post's folder sits directly in posts/
            await PostsAsync(
                [.. byPath.Select(entry => (entry.Key, entry.Value))],
                folder => CollectedPaths.FolderOf(folder) == WebsiteExportArchive.PostsFolder,
                readText
            ),
            await LookAsync(lookFiles, readText)
        );
    }

    // look.json and the files beside it, found as website.json is
    public static async Task<LookImportFolder> LookAsync(IReadOnlyList<CollectedFile> files, ReadCollectedText readText) =>
        DownloadFiles(files, LookExportArchive.FileName) is { } byPath ? await LookAsync(byPath, readText) : LookImportFolder.Empty;

    private static async Task<LookImportFolder> LookAsync(Dictionary<string, CollectedFile> byPath, ReadCollectedText readText) =>
        new(
            byPath.TryGetValue(LookExportArchive.FileName, out var json) ? await TextAsync(json, MaximumPostFileBytes, readText) : null,
            byPath.Where(entry => entry.Key != LookExportArchive.FileName).ToDictionary(entry => entry.Key, entry => entry.Value.Id)
        );

    // every file inside the folder the shallowest file of this name is in, by its path there; null
    // when there's none
    private static Dictionary<string, CollectedFile>? DownloadFiles(IReadOnlyList<CollectedFile> files, string fileName)
    {
        var named = files
            .Where(file => CollectedPaths.NameOf(file.Path) == fileName)
            .MinBy(file => file.Path.Trim('/').Count(character => character == '/'));

        if (named is null)
        {
            return null;
        }

        var root = CollectedPaths.FolderOf(named.Path);
        var byPath = new Dictionary<string, CollectedFile>();

        foreach (var file in files)
        {
            if (CollectedPaths.Inside(root, file.Path) is { } path)
            {
                byPath[path] = file;
            }
        }

        return byPath;
    }

    // one over the limit reads as empty, which the review reports as unreadable
    private static async Task<string> TextAsync(CollectedFile file, long maximumBytes, ReadCollectedText readText) =>
        file.Size > maximumBytes ? "" : await readText(file, maximumBytes);
}
