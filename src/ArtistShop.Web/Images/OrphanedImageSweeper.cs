using ArtistShop.Web.Database.Repositories;

namespace ArtistShop.Web.Images;

public record OrphanedImageSweepSettings(TimeSpan GracePeriod, TimeSpan Interval);

public class OrphanedImageSweeper(
    ImageStoragePaths paths,
    ShopItemImageRepository imageRepository,
    OrphanedImageSweepSettings settings,
    // labels messages about this class with the class's
    // full name. controls exist in appsettings.json "Logging:LogLevel"
    ILogger<OrphanedImageSweeper> logger
)
{
    public async Task SweepAsync()
    {
        var uploadedBefore = DateTimeOffset.UtcNow - settings.GracePeriod;

        var candidateKeys = FindStorageKeysUploadedBefore(uploadedBefore);

        if (candidateKeys.Count is 0)
        {
            return;
        }

        var referencedKeys = await imageRepository.GetAllRelativePathsAsync();

        var deletedCount = 0;
        foreach (var storageKey in candidateKeys)
        {
            if (referencedKeys.Contains(storageKey))
            {
                continue;
            }

            try
            {
                // why this is sync? is deleting sync
                // in the OS?
                DeleteStoredFiles(storageKey);
                deletedCount += 1;
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(
                    exception,
                    "Could not delete orphaned image {StorageKey}",
                    storageKey
                );
            }
        }
    }

    private HashSet<string> FindStorageKeysUploadedBefore(DateTimeOffset uploadedBefore)
    {
        var entries = Directory
            .EnumerateFiles(paths.Originals)
            .Concat(Directory.EnumerateDirectories(paths.Variants));
        var storageKeys = new HashSet<string>();

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);

            // how does this prevent deleting something "this code"
            // did not create
            if (!TryGetUploadTime(name, out var uploadedAt))
            {
                logger.LogWarning("Skipping {Entry}, its name is not an image storage key", entry);
                continue;
            }

            if (uploadedAt < uploadedBefore)
            {
                storageKeys.Add(name);
            }
        }

        return storageKeys;
    }

    // how does out work ?
    private static bool TryGetUploadTime(string name, out DateTimeOffset uploadedAt)
    {
        if (!Guid.TryParseExact(name, "N", out var guid) || guid.Version is not 7)
        {
            uploadedAt = default;
            return false;
        }

        var radix = 16;
        // why range is from 0 to 12?
        var millisecondsSince1970 = Convert.ToInt64(name[..12], radix);
        uploadedAt = DateTimeOffset.FromUnixTimeMilliseconds(millisecondsSince1970);
        return true;
    }

    private void DeleteStoredFiles(string storageKey)
    {
        File.Delete(Path.Combine(paths.Originals, storageKey));
        // trying to delete a directory that doesn't exist
        // will throw
        var variantDirectory = Path.Combine(paths.Variants, storageKey);
        if (Directory.Exists(variantDirectory))
        {
            Directory.Delete(variantDirectory);
        }
    }
}
