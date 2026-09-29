using ArtistShop.Web.Images;

namespace ArtistShop.Web.Exports;

// An image download, one part or all of them
public static class ImageExportArchive
{
    // Each file is read from the start again after its first bytes, so this streams every original
    // once. An original gone since the page listed it (the artwork was deleted) is left out
    public static async Task WriteAsync(
        Stream destination,
        IEnumerable<ImageExportEntry> entries,
        string folderName,
        ImageStorage imageStorage,
        CancellationToken cancellationToken
    )
    {
        await using var zip = await ExportZip.CreateAsync(destination, cancellationToken);

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

            await ExportZip.AddFileAsync(
                zip,
                $"{folderName}/{entry.PathWithoutExtension}{extension}",
                original,
                ExportZip.CompressionFor(format),
                cancellationToken
            );
        }
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
