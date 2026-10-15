namespace ArtistShop.Web.Images;

using System.Globalization;
using System.Security.Cryptography;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Utilities;

public record StoredOriginal(string StorageKey, DateTimeOffset SavedAt);

public record StoredVariant(Stream Content, DateTimeOffset LastModified);

// An original as a file libvips can read. Here it's the stored file itself; where originals aren't
// local files, it would be a download removed on Dispose
public sealed class LocalOriginal(string path) : IDisposable
{
    public string Path { get; } = path;

    public void Dispose() { }
}

// One site's images, kept apart from every other site's, so nothing that works on one site's files
// (the sweep, a quota, erasing a closed site) can reach another's. Callers name images by storage key
// and variants by ImageVariants.FileName; where and how they're kept stays in here
public class ImageStorage(string siteRootPath)
{
    public static ImageStorage ForSite(ImageStorageSettings settings, SiteId siteId) =>
        new(Path.Combine(SitesFolder(settings), siteId.Value.ToString(CultureInfo.InvariantCulture)));

    // every site that has image storage, whether or not it's listed
    public static List<SiteId> SiteIdsWithStorage(ImageStorageSettings settings) =>
        Directory.Exists(SitesFolder(settings))
            ?
            [
                .. Directory
                    .EnumerateDirectories(SitesFolder(settings))
                    .Select(path => SiteIdFromFolderName(Path.GetFileName(path)))
                    .OfType<SiteId>(),
            ]
            : [];

    private static string SitesFolder(ImageStorageSettings settings) => Path.Combine(settings.RootPath, "sites");

    private static SiteId? SiteIdFromFolderName(string name) =>
        int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? new SiteId(id) : null;

    // version 7 embeds the time it was made, so keys sort by date and don't fragment a database index.
    // "n" gives 32 hexadecimal characters with no dashes or braces
    public static string NewStorageKey() => Guid.CreateVersion7().ToString("n");

    // "N" is the same 32 hexadecimal characters NewStorageKey makes
    private static bool IsStorageKey(string? name) => Guid.TryParseExact(name, "N", out _);

    private string Originals { get; } = Path.Combine(siteRootPath, "originals");
    private string Variants { get; } = Path.Combine(siteRootPath, "variants");

    // safe to run again
    public void Create()
    {
        Directory.CreateDirectory(Originals);
        Directory.CreateDirectory(Variants);
    }

    // every image the site has, when it's erased. Nothing happens if it's already gone
    public void DeleteAll()
    {
        if (Directory.Exists(siteRootPath))
        {
            Directory.Delete(siteRootPath, recursive: true);
        }
    }

    private string OriginalPath(string storageKey) => Path.Combine(Originals, storageKey);

    private string VariantDirectory(string storageKey) => Path.Combine(Variants, storageKey);

    public bool OriginalExists(string? storageKey) =>
        storageKey is not null && IsStorageKey(storageKey) && File.Exists(OriginalPath(storageKey));

    // every original, skipping anything not named like an upload
    public IEnumerable<StoredOriginal> ListOriginals() =>
        Directory
            .EnumerateFiles(Originals)
            .Where(path => IsStorageKey(Path.GetFileName(path)))
            .Select(path => new StoredOriginal(Path.GetFileName(path), File.GetLastWriteTimeUtc(path)));

    // null when it's gone
    public long? OriginalSize(string storageKey)
    {
        var file = new FileInfo(OriginalPath(storageKey));
        return file.Exists ? file.Length : null;
    }

    // null when it's gone
    public Stream? OpenOriginal(string storageKey) =>
        File.Exists(OriginalPath(storageKey)) ? OpenRead(OriginalPath(storageKey)) : null;

    public async Task<string> OriginalSha256Async(string storageKey, CancellationToken cancellationToken)
    {
        await using var original = OpenRead(OriginalPath(storageKey));
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(original, cancellationToken));
    }

    public LocalOriginal UseLocalOriginal(string storageKey) => new(OriginalPath(storageKey));

    // Returns the SHA-256 of what was saved. An original is never written again, so the hash stays
    // true for its storage key
    public async Task<string> SaveOriginalAsync(
        Stream contentStream,
        string storageKey,
        CancellationToken cancellationToken
    )
    {
        await using var destination = File.Create(OriginalPath(storageKey));
        return await Sha256Copy.CopyAsync(contentStream, destination, cancellationToken);
    }

    // Written under a temporary name and then renamed, so a crash part way never leaves a variant
    // that looks finished
    public void SaveVariant(string storageKey, string fileName, ReadOnlySpan<byte> content)
    {
        var directory = VariantDirectory(storageKey);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        var temporaryPath = path + ".partial";

        File.WriteAllBytes(temporaryPath, content);
        File.Move(temporaryPath, path, overwrite: true);
    }

    // null when there's no such variant
    public StoredVariant? OpenVariant(string storageKey, string fileName)
    {
        var file = new FileInfo(Path.Combine(VariantDirectory(storageKey), fileName));
        return file.Exists ? new StoredVariant(OpenRead(file.FullName), file.LastWriteTimeUtc) : null;
    }

    // every image that has variants, whether or not its original is still there
    public IEnumerable<string> ListKeysWithVariants() =>
        Directory.Exists(Variants)
            ? Directory.EnumerateDirectories(Variants).Select(Path.GetFileName).OfType<string>().Where(IsStorageKey)
            : [];

    // the finished variants' names, such as 800.avif
    public List<string> VariantFileNames(string storageKey)
    {
        var directory = VariantDirectory(storageKey);

        return Directory.Exists(directory)
            ?
            [
                .. Directory
                    .EnumerateFiles(directory)
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Where(name => ImageVariants.ReadFileName(name) is not null),
            ]
            : [];
    }

    public void Delete(string storageKey)
    {
        // trying to delete a directory that doesn't exist
        // will throw
        var variantDirectory = VariantDirectory(storageKey);
        if (Directory.Exists(variantDirectory))
        {
            Directory.Delete(variantDirectory, recursive: true);
        }

        // delete originals last because sweep looks for originals
        // to determine orphans
        File.Delete(OriginalPath(storageKey));
    }

    // async, since the exports copy these into the response, which refuses a synchronous write
    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81_920, useAsync: true);
}
