namespace ArtistShop.Web.Images;

// In memory: ImageSizeQueueWorker finds what a restart lost by looking for missing sizes. Each image's
// small sizes come before any image's larger ones, since a list of new uploads shows the small ones
// first. A width stays pending from Enqueue until MarkDone, and after a failure, until a restart
// queues it again
public sealed class BackgroundImageSizeQueue : ImageSizeQueue
{
    // what an admin list and a small browse tile ask for
    private const int SmallSizeWidth = 400;

    private readonly Lock _lock = new();
    private readonly Queue<ImageSizeJob> _smallSizes = new();
    private readonly Queue<ImageSizeJob> _largerSizes = new();
    private readonly Dictionary<string, HashSet<int>> _pendingWidthsByStorageKey = [];

    // one count per job waiting, so a taker waits only while both queues are empty
    private readonly SemaphoreSlim _waitingCount = new(0);

    public override void Enqueue(ImageSizeJob job)
    {
        int[] small = [.. job.Widths.Where(width => width <= SmallSizeWidth)];
        int[] larger = [.. job.Widths.Where(width => width > SmallSizeWidth)];

        lock (_lock)
        {
            if (job.Widths.Count > 0)
            {
                PendingWidthsOf(job.StorageKey).UnionWith(job.Widths);
            }

            if (small.Length > 0)
            {
                _smallSizes.Enqueue(job with { Widths = small });
                _waitingCount.Release();
            }

            if (larger.Length > 0)
            {
                _largerSizes.Enqueue(job with { Widths = larger });
                _waitingCount.Release();
            }
        }
    }

    public override IReadOnlySet<int> PendingWidths(string storageKey)
    {
        lock (_lock)
        {
            // a copy, since the worker changes the set
            return _pendingWidthsByStorageKey.TryGetValue(storageKey, out var pendingWidths) ? new HashSet<int>(pendingWidths) : new HashSet<int>();
        }
    }

    // the job's widths are made, or no longer wanted
    public void MarkDone(ImageSizeJob job)
    {
        lock (_lock)
        {
            if (!_pendingWidthsByStorageKey.TryGetValue(job.StorageKey, out var pendingWidths))
            {
                return;
            }

            pendingWidths.ExceptWith(job.Widths);

            if (pendingWidths.Count > 0)
            {
                return;
            }

            _pendingWidthsByStorageKey.Remove(job.StorageKey);
        }

        // outside the lock, so a handler that reads the queue doesn't wait on it
        OnSizesFinished(job.StorageKey);
    }

    private HashSet<int> PendingWidthsOf(string storageKey)
    {
        if (!_pendingWidthsByStorageKey.TryGetValue(storageKey, out var pendingWidths))
        {
            pendingWidths = [];
            _pendingWidthsByStorageKey[storageKey] = pendingWidths;
        }

        return pendingWidths;
    }

    public async Task<ImageSizeJob> TakeAsync(CancellationToken cancellationToken)
    {
        await _waitingCount.WaitAsync(cancellationToken);

        lock (_lock)
        {
            return _smallSizes.Count > 0 ? _smallSizes.Dequeue() : _largerSizes.Dequeue();
        }
    }
}
