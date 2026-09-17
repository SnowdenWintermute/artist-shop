using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Images;

// read from appsettings.json by ValidatedSettings; the ranges also catch a missing value
public sealed record ImageProcessingSettings
{
    [Range(1, 1000)]
    public int MaximumMegapixels { get; init; }

    [Range(1.0, 100.0)]
    public double MemoryEstimateMultiplier { get; init; }

    // megabytes that image processing may use at once; the rest of the container's memory is the
    // app's own. Sized per deployment, like imgproxy's worker count
    [Range(64, 1_000_000)]
    public int MemoryBudgetMegabytes { get; init; }

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan ProxyTimeout { get; init; }

    [Range(typeof(TimeSpan), "00:00:00.100", "00:10:00")]
    public TimeSpan EstimatedTimePerImage { get; init; }

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan BusyRetryAfter { get; init; }
}

public record ImageProcessingCapacity(int ProcessorSlots, int MemoryBudgetMegabytes, int QueuedImageLimit)
{
    public static ImageProcessingCapacity FromHost(ImageProcessingSettings settings) =>
        For(
            settings,
            Environment.ProcessorCount,
            // what .NET may put on its own heap: the machine's memory, or inside a container the
            // share of its limit that DOTNET_GCHeapHardLimitPercent sets (75% when nothing does)
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / Units.BytesPerMebibyte
        );

    public static ImageProcessingCapacity For(
        ImageProcessingSettings settings,
        int processorCount,
        long heapMemoryMegabytes
    )
    {
        // one core stays free for serving pages; a one-core machine still gets one slot
        var processorSlots = Math.Max(1, processorCount - 1);

        // libvips allocates outside the managed heap, so this only catches a budget larger than the
        // whole heap allowance. Holding both is the container limit's job, not this check's
        if (settings.MemoryBudgetMegabytes >= heapMemoryMegabytes)
        {
            throw new InvalidOperationException(
                $"ImageProcessing:MemoryBudgetMegabytes is {settings.MemoryBudgetMegabytes}, but .NET may use only "
                    + $"{heapMemoryMegabytes} MB of memory in total."
            );
        }

        // enough waiting images that the last one still finishes before the proxy gives up on it
        var queuedImageLimit = (int)(processorSlots * (settings.ProxyTimeout / settings.EstimatedTimePerImage));

        return new ImageProcessingCapacity(processorSlots, settings.MemoryBudgetMegabytes, queuedImageLimit);
    }
}

public class ImageProcessingBusyException(TimeSpan retryAfter)
    : Exception("The server is busy processing other images. Try again shortly.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

// Two limits: a processor slot per image, so images don't fight over the cores, and megabytes
// of estimated memory, so a few huge images can't run the machine out of memory. An image takes
// its slot first and then waits for memory, so at most ProcessorSlots images wait for memory and
// only the slot queue needs a limit.
public sealed class ImageProcessingLimiter
{
    private readonly ConcurrencyLimiter _processorLimiter;
    private readonly ConcurrencyLimiter _memoryLimiter;
    private readonly TimeSpan _busyRetryAfter;

    public int MemoryBudgetMegabytes { get; }

    public ImageProcessingLimiter(ImageProcessingCapacity capacity, TimeSpan busyRetryAfter)
    {
        MemoryBudgetMegabytes = capacity.MemoryBudgetMegabytes;
        _busyRetryAfter = busyRetryAfter;

        _processorLimiter = new ConcurrencyLimiter(
            new ConcurrencyLimiterOptions
            {
                PermitLimit = capacity.ProcessorSlots,
                QueueLimit = capacity.QueuedImageLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }
        );

        _memoryLimiter = new ConcurrencyLimiter(
            new ConcurrencyLimiterOptions
            {
                PermitLimit = capacity.MemoryBudgetMegabytes,
                // each waiting image holds a slot and asks for at most the whole budget
                QueueLimit = capacity.MemoryBudgetMegabytes * capacity.ProcessorSlots,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }
        );
    }

    public async Task<IDisposable> AcquireAsync(int megabytes, CancellationToken cancellationToken)
    {
        // a request for more permits than the limit throws, so the caller must turn such images away first
        if (megabytes > MemoryBudgetMegabytes)
        {
            throw new ArgumentOutOfRangeException(nameof(megabytes), megabytes, "More than the whole memory budget.");
        }

        var processorLease = await _processorLimiter.AcquireAsync(permitCount: 1, cancellationToken);

        if (!processorLease.IsAcquired)
        {
            processorLease.Dispose();
            throw new ImageProcessingBusyException(_busyRetryAfter);
        }

        try
        {
            var memoryLease = await _memoryLimiter.AcquireAsync(megabytes, cancellationToken);

            if (!memoryLease.IsAcquired)
            {
                // only reachable if the memory queue is smaller than the slots allow, which the
                // constructor sizes so it can't be. Processing without a memory permit would
                // defeat the budget silently, so it stops here instead
                memoryLease.Dispose();
                throw new InvalidOperationException(
                    $"The memory queue turned away a request for {megabytes} MB, so its limit is too small "
                        + "for the number of processor slots."
                );
            }

            return new Lease(processorLease, memoryLease);
        }
        catch
        {
            // cancelled while waiting for memory: give the slot back
            processorLease.Dispose();
            throw;
        }
    }

    // disposing hands back both the memory and the slot
    private sealed class Lease(RateLimitLease processorLease, RateLimitLease memoryLease) : IDisposable
    {
        public void Dispose()
        {
            memoryLease.Dispose();
            processorLease.Dispose();
        }
    }
}
