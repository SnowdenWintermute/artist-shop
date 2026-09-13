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

        logger.LogInformation("Removed {deletedCount} orphaned images", deletedCount);
    }

    private List<string> FindStorageKeysUploadedBefore(DateTimeOffset uploadedBefore)
    {
        var storageKeys = new List<string>();

        foreach (var originalPath in Directory.EnumerateFiles(paths.Originals))
        {
            var name = Path.GetFileName(originalPath);

            // skip anything not named like an upload
            if (!Guid.TryParseExact(name, "N", out _))
            {
                continue;
            }

            // the file system records the modified time as a DateTime in UTC,
            // so compare against the UTC DateTime form of the cutoff
            if (File.GetLastWriteTimeUtc(originalPath) < uploadedBefore.UtcDateTime)
            {
                storageKeys.Add(name);
            }
        }

        return storageKeys;
    }

    private void DeleteStoredFiles(string storageKey)
    {
        // trying to delete a directory that doesn't exist
        // will throw
        var variantDirectory = Path.Combine(paths.Variants, storageKey);
        if (Directory.Exists(variantDirectory))
        {
            Directory.Delete(variantDirectory, recursive: true);
        }

        // delete originals last because sweep looks for originals
        // to determine orphans
        File.Delete(Path.Combine(paths.Originals, storageKey));
    }
}
