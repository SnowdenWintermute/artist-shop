namespace ArtistShop.Web.Images;

public record ImageSizeJob(ImageStorage ImageStorage, string StorageKey, IReadOnlyList<int> Widths);

// Where an upload hands over the sizes it didn't wait for. BackgroundImageSizeQueue in the app, which
// ImageSizeQueueWorker empties; the tests' queue makes them at once
public abstract class ImageSizeQueue
{
    public abstract void Enqueue(ImageSizeJob job);

    // once an image has every size, with its storage key. Raised on the worker's thread
    public event Action<string>? SizesFinished;

    protected void OnSizesFinished(string storageKey) => SizesFinished?.Invoke(storageKey);

    // the widths queued and not made yet; empty for an image with every size
    public abstract IReadOnlySet<int> PendingWidths(string storageKey);

    public bool IsProcessing(string storageKey) => PendingWidths(storageKey).Count > 0;

    // The widths a page may ask for now: every one the image will have, less those still being made.
    // Never empty, since an upload makes the smallest before it answers
    public int[] ReadyWidths(string storageKey, int imageWidth)
    {
        var pendingWidths = PendingWidths(storageKey);
        return [.. ImageVariants.WidthsFor(imageWidth).Where(width => !pendingWidths.Contains(width))];
    }
}
