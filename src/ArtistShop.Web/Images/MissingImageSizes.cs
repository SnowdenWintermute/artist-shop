using ArtistShop.Web.Domain.Sites;

namespace ArtistShop.Web.Images;

// What a restart left unmade: a job for each image of a site still marked as waiting for sizes. Only
// marked images are opened, so this takes as long as the site has unfinished images, not images
public class MissingImageSizes(ImageStorageSettings storageSettings, ILogger<MissingImageSizes> logger)
{
    public List<ImageSizeJob> Find(SiteId siteId)
    {
        var imageStorage = ImageStorage.ForSite(storageSettings, siteId);
        var processor = new ImageProcessor(imageStorage);
        var jobs = new List<ImageSizeJob>();

        // one with no original is the sweep's to remove
        foreach (var storageKey in imageStorage.ListKeysWithSizesPending().Where(imageStorage.OriginalExists))
        {
            // each image on its own, so an unreadable original doesn't stop the rest
            try
            {
                var missingWidths = processor.FindMissingSizes(storageKey);

                if (missingWidths.Length > 0)
                {
                    jobs.Add(new ImageSizeJob(imageStorage, storageKey, missingWidths));
                }
                // the restart came between its last size and clearing the mark
                else
                {
                    imageStorage.ClearSizesPending(storageKey);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Couldn't check the sizes of image {StorageKey}", storageKey);
            }
        }

        return jobs;
    }
}
