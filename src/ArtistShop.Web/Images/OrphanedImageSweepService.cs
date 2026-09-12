namespace ArtistShop.Web.Images;

public class OrphanedImageSweepService(
    IServiceScopeFactory scopeFactory,
    OrphanedImageSweepSettings settings,
    ILogger<OrphanedImageSweepService> logger
) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // "ticks" once per interval
        using var timer = new PeriodicTimer(settings.Interval);

        // do{
        //     await SweepOnceAsync();
        // }while (await )
    }

    private Task SweepOnceAsync()
    {
        // try{

        // }
    }
}
