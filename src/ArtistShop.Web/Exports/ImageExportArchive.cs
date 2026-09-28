using System.IO.Compression;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Exports;

// An image download, one part or all of them, written into the response as it's read from disk,
// so a download of many gigabytes never sits whole in memory or on disk
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
        // async all the way: ASP.NET refuses a synchronous write to the response
        await using var zip = await ZipArchive.CreateAsync(
            destination,
            ZipArchiveMode.Create,
            leaveOpen: true,
            entryNameEncoding: null,
            cancellationToken
        );

        foreach (var entry in entries)
        {
            if (!imageStorage.OriginalExists(entry.Image.StorageKey))
            {
                continue;
            }

            await using var original = new FileStream(
                imageStorage.OriginalPath(entry.Image.StorageKey),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                useAsync: true
            );

            var header = new byte[ExportImageFormat.HeaderLength];
            var headerLength = await original.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
            original.Position = 0;

            // uploads are checked by libvips, not by these first bytes, so an original can be in a
            // format this doesn't recognise; its uploaded name's extension is the best guess then
            var format = ExportImageFormat.Detect(header.AsSpan(0, headerLength));
            var extension = format?.Extension ?? Path.GetExtension(entry.Image.OriginalFileName) ?? "";
            var compression = format is { IsCompressed: true } ? CompressionLevel.NoCompression : CompressionLevel.Optimal;

            var zipEntry = zip.CreateEntry($"{folderName}/{entry.PathWithoutExtension}{extension}", compression);
            await using var target = await zipEntry.OpenAsync(cancellationToken);
            await original.CopyToAsync(target, cancellationToken);
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
