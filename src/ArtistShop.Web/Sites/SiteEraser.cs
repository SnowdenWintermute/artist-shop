namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;

// Erases the sites whose grace period is over: the schema, the image folders, then the platform's
// row, and with it the hosts, which can then be signed up for again. The row goes last, so a site
// erased partway is still due and is finished next time; every step can run again. Until then,
// a restart's SiteProvisioner.Prepare may make its schema again, empty, for this to drop
public sealed class SiteEraser(
    SiteRepository siteRepository,
    SiteSchemas siteSchemas,
    ImageStorageSettings imageStorageSettings,
    TimeProvider timeProvider,
    ILogger<SiteEraser> logger
)
{
    public async Task EraseDueAsync()
    {
        foreach (var siteId in await siteRepository.GetDueForErasingAsync(timeProvider.GetUtcNow()))
        {
            // each site on its own, so one failing doesn't stop the rest being erased
            try
            {
                await siteSchemas.DropAsync(siteId);
                ImageStorage.ForSite(imageStorageSettings, siteId).DeleteAll();
                await siteRepository.EraseAsync(siteId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Erasing site {SiteId} failed", siteId.Value);
            }
        }
    }
}
