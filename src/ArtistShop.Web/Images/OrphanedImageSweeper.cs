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
    OrphanedImageSweepSettings settings,
    TimeProvider timeProvider,
    // labels messages about this class with the class's
    // full name. controls exist in appsettings.json "Logging:LogLevel"
    ILogger<OrphanedImageSweeper> logger
)
{
    // One site: its image folder against its own database, so another site's files are never
    // candidates, and never compared with a database that doesn't know them
    public async Task SweepAsync(
        ImageStorage imageStorage,
        WorkImageRepository imageRepository,
        PostRepository postRepository,
        CustomComponentRepository customComponentRepository,
        BrandImageRepository brandImageRepository
    )
    {
        var uploadedBefore = timeProvider.GetUtcNow() - settings.GracePeriod;

        var candidateKeys = imageStorage
            .ListOriginals()
            .Where(original => original.SavedAt < uploadedBefore)
            .Select(original => original.StorageKey)
            .ToList();

        // with no grace period: an upload saves its original before any size or mark, so sizes or a
        // mark without one are always left over, such as from an image deleted while its sizes were made
        var orphanedKeys = imageStorage
            .ListKeysWithVariants()
            .Union(imageStorage.ListKeysWithSizesPending())
            .Where(storageKey => !imageStorage.OriginalExists(storageKey))
            .ToList();

        if (candidateKeys.Count > 0)
        {
            // a work's images, the ones uploaded into posts and text components, and the logos' and favicon's originals
            var referencedKeys = await imageRepository.GetAllStorageKeysAsync();
            referencedKeys.UnionWith(await postRepository.GetAllImageStorageKeysAsync());
            referencedKeys.UnionWith(await customComponentRepository.GetAllImageStorageKeysAsync());
            referencedKeys.UnionWith(await brandImageRepository.GetAllStorageKeysAsync());

            orphanedKeys.AddRange(candidateKeys.Where(storageKey => !referencedKeys.Contains(storageKey)));
        }

        var deletedCount = 0;
        foreach (var storageKey in orphanedKeys)
        {
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
}
