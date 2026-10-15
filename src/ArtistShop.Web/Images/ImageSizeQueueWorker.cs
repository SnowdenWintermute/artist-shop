using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Sites;

namespace ArtistShop.Web.Images;

// Makes the sizes uploads didn't wait for, one job at a time, under the same limiter as uploads. At
// startup it first queues what a restart lost: every size missing from an image a site uses
public sealed class ImageSizeQueueWorker(
    BackgroundImageSizeQueue queue,
    ImageProcessingLimiter processingLimiter,
    ImageProcessingSettings processingSettings,
    MissingImageSizes missingImageSizes,
    SiteRepository siteRepository,
    ILogger<ImageSizeQueueWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await QueueMissingSizesAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            ImageSizeJob job;

            try
            {
                job = await queue.TakeAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await MakeSizesAsync(job, stoppingToken);
        }
    }

    private async Task MakeSizesAsync(ImageSizeJob job, CancellationToken stoppingToken)
    {
        var imageStorage = job.ImageStorage;
        var processor = new ImageProcessor(imageStorage);

        try
        {
            // deleted since it was queued
            if (!imageStorage.OriginalExists(job.StorageKey))
            {
                queue.MarkDone(job);
                return;
            }

            using (
                await ImageProcessingAdmission.AcquireAsync(
                    processor.ReadHeader(job.StorageKey),
                    processingLimiter,
                    processingSettings,
                    stoppingToken
                )
            )
            {
                // an image deleted while these are made leaves them without an original, for the sweep
                processor.MakeSizes(job.StorageKey, job.Widths);
            }

            queue.MarkDone(job);
        }
        // uploads have every slot and the queue for them is full; this job waits its turn again
        catch (ImageProcessingBusyException exception)
        {
            try
            {
                await Task.Delay(exception.RetryAfter, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            queue.Enqueue(job);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Couldn't make the sizes of image {StorageKey}", job.StorageKey);
        }
    }

    private async Task QueueMissingSizesAsync()
    {
        List<SiteId> siteIds;

        try
        {
            siteIds = await siteRepository.GetIdsAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Couldn't read the list of sites to find images missing sizes");
            return;
        }

        foreach (var siteId in siteIds)
        {
            // each site on its own, so one failing doesn't stop the rest
            try
            {
                foreach (var job in await missingImageSizes.FindAsync(siteId))
                {
                    queue.Enqueue(job);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Couldn't find images missing sizes for site {SiteId}", siteId.Value);
            }
        }
    }
}
