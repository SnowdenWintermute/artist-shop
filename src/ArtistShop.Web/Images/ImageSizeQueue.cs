namespace ArtistShop.Web.Images;

public record ImageSizeJob(ImageStorage ImageStorage, string StorageKey, IReadOnlyList<int> Widths);

// Where an upload hands over the sizes it didn't wait for. BackgroundImageSizeQueue in the app, which
// ImageSizeQueueWorker empties; the tests' queue makes them at once
public abstract class ImageSizeQueue(ILogger logger)
{
    public abstract void Enqueue(ImageSizeJob job);

    // once an image has every size, with its storage key. Raised on the worker's thread
    public event Action<string>? SizesFinished;

    protected void OnSizesFinished(string storageKey)
    {
        // each on its own, so one that throws doesn't keep the news from the rest
        foreach (var handler in Delegate.EnumerateInvocationList(SizesFinished))
        {
            try
            {
                handler(storageKey);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "A listener failed on hearing image {StorageKey} has every size", storageKey);
            }
        }
    }

    // the widths queued and not made yet; empty for an image with every size
    public abstract IReadOnlySet<int> PendingWidths(string storageKey);

    // whether a picture that uses sizes up to maximumWidth will get sharper
    public bool IsProcessing(string storageKey, int maximumWidth) =>
        PendingWidths(storageKey).Any(width => width <= maximumWidth);

    // The widths a page may ask for now: every one the image will have, less those still being made.
    // Never empty, since an upload makes the smallest before it answers
    public int[] ReadyWidths(string storageKey, int imageWidth)
    {
        var pendingWidths = PendingWidths(storageKey);
        return [.. ImageVariants.WidthsFor(imageWidth).Where(width => !pendingWidths.Contains(width))];
    }
}
