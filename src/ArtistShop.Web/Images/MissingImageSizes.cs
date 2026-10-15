using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;

namespace ArtistShop.Web.Images;

// What a restart left unmade: a job for each image a site uses that's missing one of its sizes. A
// work's images and the ones uploaded into posts and text components; brand images have no sizes
public class MissingImageSizes(
    SiteDatabases siteDatabases,
    ImageStorageSettings storageSettings,
    ILogger<MissingImageSizes> logger
)
{
    public async Task<List<ImageSizeJob>> FindAsync(SiteId siteId)
    {
        var database = siteDatabases.For(siteId);
        var storageKeys = await new WorkImageRepository(database).GetAllStorageKeysAsync();
        storageKeys.UnionWith(await new PostRepository(database).GetAllImageStorageKeysAsync());
        storageKeys.UnionWith(await new CustomComponentRepository(database).GetAllImageStorageKeysAsync());

        var imageStorage = ImageStorage.ForSite(storageSettings, siteId);
        var processor = new ImageProcessor(imageStorage);
        var jobs = new List<ImageSizeJob>();

        foreach (var storageKey in storageKeys.Where(imageStorage.OriginalExists))
        {
            // each image on its own, so an unreadable original doesn't stop the rest
            try
            {
                var missingWidths = processor.FindMissingSizes(storageKey);

                if (missingWidths.Length > 0)
                {
                    jobs.Add(new ImageSizeJob(imageStorage, storageKey, missingWidths));
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
