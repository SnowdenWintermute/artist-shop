namespace ArtistShop.Web.Images;

// Makes the sizes uploads didn't wait for, one job at a time, under the same limiter as uploads. At
// startup it first queues what a restart lost
public sealed class ImageSizeQueueWorker(
    BackgroundImageSizeQueue queue,
    ImageProcessingLimiter processingLimiter,
    ImageProcessingSettings processingSettings,
    MissingImageSizes missingImageSizes,
    ImageStorageSettings storageSettings,
    ILogger<ImageSizeQueueWorker> logger
) : BackgroundService
{
    // The app answers no request until every hosted service has started, so no page is shown sizes
    // the queue doesn't yet know are missing, and no upload is queued twice
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        QueueMissingSizes();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

    private void QueueMissingSizes()
    {
        foreach (var siteId in ImageStorage.SiteIdsWithStorage(storageSettings))
        {
            // each site on its own, so one failing doesn't stop the rest
            try
            {
                foreach (var job in missingImageSizes.Find(siteId))
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
