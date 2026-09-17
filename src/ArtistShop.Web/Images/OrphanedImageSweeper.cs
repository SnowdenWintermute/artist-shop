using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Database.Repositories;

namespace ArtistShop.Web.Images;

// read from appsettings.json by ValidatedSettings; the ranges also catch a missing value
public sealed record OrphanedImageSweepSettings
{
    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan GracePeriod { get; init; }

    [Range(typeof(TimeSpan), "00:00:01", "30.00:00:00")]
    public TimeSpan Interval { get; init; }
}

public class OrphanedImageSweeper(
    ImageStorage imageStorage,
    ArtworkImageRepository imageRepository,
    OrphanedImageSweepSettings settings,
    TimeProvider timeProvider,
    // labels messages about this class with the class's
    // full name. controls exist in appsettings.json "Logging:LogLevel"
    ILogger<OrphanedImageSweeper> logger
)
{
    public async Task SweepAsync()
    {
        var uploadedBefore = timeProvider.GetUtcNow() - settings.GracePeriod;

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
                imageStorage.Delete(storageKey);
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

        if (deletedCount > 0)
        {
#pragma warning disable CA1873
            logger.LogInformation("Removed {DeletedCount} orphaned images", deletedCount);
#pragma warning restore CA1873
        }
    }

    private List<string> FindStorageKeysUploadedBefore(DateTimeOffset uploadedBefore)
    {
        var storageKeys = new List<string>();

        foreach (var originalPath in Directory.EnumerateFiles(imageStorage.Originals))
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
}
