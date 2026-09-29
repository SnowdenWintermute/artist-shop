using System.IO.Compression;
using System.Text;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Exports;

// The downloads written into the response as they're read from disk, so a large one never sits
// whole in memory or on disk. Async all the way: ASP.NET refuses a synchronous write to the response
public static class ExportZip
{
    public const string ReadmeFileName = "README.txt";

    // the byte order mark tells Excel a CSV file is UTF-8, and the imports skip it
    public static readonly UTF8Encoding Utf8WithByteOrderMark = new(encoderShouldEmitUTF8Identifier: true);

    public static Task<ZipArchive> CreateAsync(Stream destination, CancellationToken cancellationToken) =>
        ZipArchive.CreateAsync(destination, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, cancellationToken);

    public static Stream OpenFile(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81_920, useAsync: true);

    public static async Task AddFileAsync(
        ZipArchive zip,
        string path,
        Stream source,
        CompressionLevel compression,
        CancellationToken cancellationToken
    )
    {
        await using var target = await zip.CreateEntry(path, compression).OpenAsync(cancellationToken);
        await source.CopyToAsync(target, cancellationToken);
    }

    // the SHA-256 of what was copied, worked out on the way so the file is read once
    public static async Task<string> AddFileWithSha256Async(
        ZipArchive zip,
        string path,
        Stream source,
        CompressionLevel compression,
        CancellationToken cancellationToken
    )
    {
        await using var target = await zip.CreateEntry(path, compression).OpenAsync(cancellationToken);
        return await Sha256Copy.CopyAsync(source, target, cancellationToken);
    }

    public static Task AddTextAsync(ZipArchive zip, string path, string text, CancellationToken cancellationToken) =>
        AddBytesAsync(zip, path, Encoding.UTF8.GetBytes(text), cancellationToken);

    public static Task AddCsvAsync(ZipArchive zip, string path, string text, CancellationToken cancellationToken) =>
        AddBytesAsync(zip, path, [.. Utf8WithByteOrderMark.GetPreamble(), .. Utf8WithByteOrderMark.GetBytes(text)], cancellationToken);

    private static async Task AddBytesAsync(ZipArchive zip, string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var target = await zip.CreateEntry(path, CompressionLevel.Optimal).OpenAsync(cancellationToken);
        await target.WriteAsync(bytes, cancellationToken);
    }

    // already compressed formats are stored, since deflating them again costs time and saves almost
    // nothing; anything unrecognised is deflated in case it helps
    public static CompressionLevel CompressionFor(ExportImageFormat? format) =>
        format is { IsCompressed: true } ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
}
