namespace ArtistShop.Web.Sites;

// The platform's daily upkeep, at startup and then once a day: erasing deleted sites whose grace
// period is over, and expired rows. Each on its own, so one failing doesn't stop the other
public sealed class PlatformCleanupService(
    SiteEraser siteEraser,
    ExpiredRowCleanup expiredRowCleanup,
    TimeProvider timeProvider,
    ILogger<PlatformCleanupService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), timeProvider);

        do
        {
            try
            {
                await siteEraser.EraseDueAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Couldn't read which sites are due for erasing");
            }

            try
            {
                await expiredRowCleanup.DeleteAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Couldn't delete expired sign-up codes and invitations");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
