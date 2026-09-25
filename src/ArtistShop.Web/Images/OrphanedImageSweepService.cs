namespace ArtistShop.Web.Images;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;

public class OrphanedImageSweepService(
    OrphanedImageSweeper sweeper,
    ImageStorageSettings storageSettings,
    SiteRepository siteRepository,
    SiteDatabases siteDatabases,
    OrphanedImageSweepSettings settings,
    TimeProvider timeProvider,
    ILogger<OrphanedImageSweepService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // "ticks" once per interval
        using var timer = new PeriodicTimer(settings.Interval, timeProvider);

        // why are we using do while instead of just while?
        do
        {
            await SweepOnceAsync();
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepOnceAsync()
    {
        List<SiteId> siteIds;

        try
        {
            siteIds = await siteRepository.GetIdsAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Orphaned image sweep couldn't read the list of sites");
            return;
        }

        foreach (var siteId in siteIds)
        {
            // each site on its own, so one failing doesn't stop the rest being swept
            try
            {
                var database = siteDatabases.For(siteId);

                await sweeper.SweepAsync(
                    ImageStorage.ForSite(storageSettings, siteId),
                    new ArtworkImageRepository(database),
                    new PostRepository(database)
                );
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Orphaned image sweep failed for site {SiteId}", siteId.Value);
            }
        }
    }
}
