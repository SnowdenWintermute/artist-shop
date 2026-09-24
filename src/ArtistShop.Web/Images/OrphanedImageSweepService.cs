namespace ArtistShop.Web.Images;

using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;
using Npgsql;

public class OrphanedImageSweepService(
    OrphanedImageSweeper sweeper,
    ImageStorageSettings storageSettings,
    NpgsqlDataSource dataSource,
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
        // until the platform database lists the sites, the one site, in the one database
        SiteId[] siteIds = [SingleSite.Id];

        foreach (var siteId in siteIds)
        {
            // each site on its own, so one failing doesn't stop the rest being swept
            try
            {
                await sweeper.SweepAsync(
                    ImageStorage.ForSite(storageSettings, siteId),
                    new ArtworkImageRepository(dataSource),
                    new PostRepository(dataSource)
                );
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Orphaned image sweep failed for site {SiteId}", siteId.Value);
            }
        }
    }
}
