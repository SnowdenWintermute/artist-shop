namespace ArtistShop.Web.Images;

using System.Globalization;
using ArtistShop.Web.Domain.Sites;

// One site's images, in a folder of its own, so nothing that works on one site's files (the sweep,
// a quota, erasing a closed site) can reach another's
public class ImageStorage(string siteRootPath)
{
    public static ImageStorage ForSite(ImageStorageSettings settings, SiteId siteId) =>
        new(Path.Combine(SitesFolder(settings), siteId.Value.ToString(CultureInfo.InvariantCulture)));

    // every site that has a folder, whether or not it's listed
    public static List<SiteId> SiteIdsWithFolders(ImageStorageSettings settings) =>
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

    public string Originals { get; } = Path.Combine(siteRootPath, "originals");
    public string Variants { get; } = Path.Combine(siteRootPath, "variants");

    public void CreateFolders()
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

    public string OriginalPath(string storageKey) => Path.Combine(Originals, storageKey);

    public string VariantDirectory(string storageKey) => Path.Combine(Variants, storageKey);

    public bool OriginalExists(string? storageKey)
    {
        // "N" is the 32 hexadecimal characters, no dashes, format
        // same format our upload endpoint uses
        // "out _" discards parsed value, we only care if it did parse
        return Guid.TryParseExact(storageKey, "N", out _) && File.Exists(OriginalPath(storageKey));
    }

    public async Task SaveOriginalAsync(
        Stream contentStream,
        string storageKey,
        CancellationToken cancellationToken
    )
    {
        await using var destination = File.Create(OriginalPath(storageKey));
        await contentStream.CopyToAsync(destination, cancellationToken);
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
}
