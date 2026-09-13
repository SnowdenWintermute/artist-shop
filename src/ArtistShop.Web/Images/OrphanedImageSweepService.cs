namespace ArtistShop.Web.Images;

public class OrphanedImageSweepService(
    IServiceScopeFactory scopeFactory,
    OrphanedImageSweepSettings settings,
    ILogger<OrphanedImageSweepService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // "ticks" once per interval
        using var timer = new PeriodicTimer(settings.Interval);

        // why are we using do while instead of just while?
        do
        {
            await SweepOnceAsync();
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepOnceAsync()
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sweeper = scope.ServiceProvider.GetRequiredService<OrphanedImageSweeper>();
            await sweeper.SweepAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Orphaned image sweep failed");
        }
    }
}
