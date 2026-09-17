using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.Images;

public static class TestImageProcessing
{
    public static readonly ImageProcessingSettings Settings = new()
    {
        MaximumMegapixels = 100,
        MemoryEstimateMultiplier = 2,
        MemoryBudgetMegabytes = 1000,
        ProxyTimeout = TimeSpan.FromMinutes(1),
        EstimatedTimePerImage = TimeSpan.FromSeconds(10),
        BusyRetryAfter = TimeSpan.FromSeconds(10),
    };

    // room for anything the tests upload, so only tests about limits ever wait
    public static ImageProcessingLimiter CreateAmpleLimiter() =>
        new(
            new ImageProcessingCapacity(ProcessorSlots: 4, MemoryBudgetMegabytes: 4096, QueuedImageLimit: 100),
            Settings.BusyRetryAfter
        );
}
