namespace ArtistShop.Web.Sites;

using ArtistShop.Web.Identity;

// The platform's daily upkeep, at startup and then once a day: erasing deleted sites whose grace
// period is over, expired rows, ended sign-ins and expired website sign-in codes. Each on its own, so
// one failing doesn't stop the others
public sealed class PlatformCleanupService(
    SiteEraser siteEraser,
    ExpiredRowCleanup expiredRowCleanup,
    DatabaseTicketStore ticketStore,
    IServiceScopeFactory scopeFactory,
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

            try
            {
                await ticketStore.DeleteEndedAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Couldn't delete ended sign-ins");
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SiteSignIns>().DeleteExpiredAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Couldn't delete expired website sign-in codes");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
