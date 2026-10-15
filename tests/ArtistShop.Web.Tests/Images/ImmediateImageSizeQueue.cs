using System.Collections.Concurrent;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.Images;

// The tests' size queue: makes the sizes at once, so a test can read every size as soon as the upload
// answers. The app's queue has tests of its own
public sealed class ImmediateImageSizeQueue : ImageSizeQueue
{
    private readonly ConcurrentDictionary<string, IReadOnlySet<int>> _heldBack = new();

    public override void Enqueue(ImageSizeJob job) =>
        new ImageProcessor(job.ImageStorage).MakeSizes(job.StorageKey, job.Widths);

    public override IReadOnlySet<int> PendingWidths(string storageKey) =>
        _heldBack.TryGetValue(storageKey, out var widths) ? widths : new HashSet<int>();

    // for a page test: the image looks as it does while these sizes are still being made
    public void HoldBack(string storageKey, params int[] widths) => _heldBack[storageKey] = widths.ToHashSet();
}
