using System.IO.Compression;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Exports;

// An image download, one part or all of them, with images.csv saying whose each file is
public static class ImageExportArchive
{
    public const string ImageListFileName = "images.csv";

    // images.csv's columns: the file's path inside the download's folder, its work, and the
    // SHA-256 of the file, which post.json names a work picture by
    public static class ImageListHeaders
    {
        public const string File = "file";
        public const string WorkType = "workType";
        public const string Title = "title";
        public const string Slug = "slug";
        public const string Sha256 = "sha256";
    }

    public static async Task WriteAsync(
        Stream destination,
        IEnumerable<ImageExportEntry> entries,
        string folderName,
        ImageStorage imageStorage,
        CancellationToken cancellationToken
    )
    {
        await using var zip = await ExportZip.CreateAsync(destination, cancellationToken);
        await AddAsync(zip, entries, folderName, imageStorage, cancellationToken);
    }

    // Each file is read from the start again after its first bytes, so this streams every original
    // once. An original gone since the page listed it (the work was deleted) is left out
    public static async Task AddAsync(
        ZipArchive zip,
        IEnumerable<ImageExportEntry> entries,
        string folderName,
        ImageStorage imageStorage,
        CancellationToken cancellationToken
    )
    {
        var imageList = new CsvText();
        imageList.AddRow([ImageListHeaders.File, ImageListHeaders.WorkType, ImageListHeaders.Title, ImageListHeaders.Slug, ImageListHeaders.Sha256]);

        foreach (var entry in entries)
        {
            if (!imageStorage.OriginalExists(entry.Image.StorageKey))
            {
                continue;
            }

            await using var original = ExportZip.OpenFile(imageStorage.OriginalPath(entry.Image.StorageKey));

            // uploads are checked by libvips, not by these first bytes, so an original can be in a
            // format this doesn't recognise; its uploaded name's extension is the best guess then
            var format = await ExportImageFormat.ReadAsync(original, cancellationToken);
            var extension = format?.Extension ?? Path.GetExtension(entry.Image.OriginalFileName) ?? "";

            var path = $"{entry.PathWithoutExtension}{extension}";
            var sha256 = await ExportZip.AddFileWithSha256Async(zip, $"{folderName}/{path}", original, ExportZip.CompressionFor(format), cancellationToken);
            imageList.AddRow([path, entry.Work.Type.Name.Value, entry.Work.Name.Value, entry.Work.Slug.Value, sha256]);
        }

        // last, since each file's hash is only known once it's been copied
        await ExportZip.AddCsvAsync(zip, $"{folderName}/{ImageListFileName}", imageList.ToString(), cancellationToken);
    }

    // what the Export page lists for a download: the originals still on disk and their total size
    public static (int Count, long Bytes) Measure(IEnumerable<ImageExportEntry> entries, ImageStorage imageStorage)
    {
        var sizes = entries
            .Where(entry => imageStorage.OriginalExists(entry.Image.StorageKey))
            .Select(entry => new FileInfo(imageStorage.OriginalPath(entry.Image.StorageKey)).Length)
            .ToList();

        return (sizes.Count, sizes.Sum());
    }
}
