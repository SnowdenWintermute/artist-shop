using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.Images;

public sealed class BackgroundImageSizeQueueTests
{
    private static readonly ImageStorage Storage = new("unused");

    [Fact]
    public async Task EveryImagesSmallSizesComeBeforeAnyLargerOnes()
    {
        var queue = new BackgroundImageSizeQueue();
        queue.Enqueue(new ImageSizeJob(Storage, "first", [400, 800, 1600]));
        queue.Enqueue(new ImageSizeJob(Storage, "second", [400, 800, 1600]));

        List<string> taken = [];
        for (var i = 0; i < 4; i++)
        {
            var job = await queue.TakeAsync(TestContext.Current.CancellationToken);
            taken.Add($"{job.StorageKey} {string.Join(" ", job.Widths)}");
        }

        Assert.Equal(["first 400", "second 400", "first 800 1600", "second 800 1600"], taken);
    }

    [Fact]
    public async Task ATakerWaitsForAJob()
    {
        var queue = new BackgroundImageSizeQueue();

        var taking = queue.TakeAsync(TestContext.Current.CancellationToken);
        Assert.False(taking.IsCompleted);

        queue.Enqueue(new ImageSizeJob(Storage, "key", [800]));

        Assert.Equal("key", (await taking).StorageKey);
    }

    // an image narrower than the smallest later width has nothing left to queue
    [Fact]
    public async Task NoWidthsQueueNothing()
    {
        var queue = new BackgroundImageSizeQueue();
        queue.Enqueue(new ImageSizeJob(Storage, "nothing", []));
        queue.Enqueue(new ImageSizeJob(Storage, "something", [800]));

        Assert.Equal("something", (await queue.TakeAsync(TestContext.Current.CancellationToken)).StorageKey);
    }

    [Fact]
    public async Task AnImagesSizesAreReadyOnceItsJobsAreDone()
    {
        var queue = new BackgroundImageSizeQueue();
        queue.Enqueue(new ImageSizeJob(Storage, "key", ImageVariants.LaterWidths(2000)));

        Assert.True(queue.IsProcessing("key"));
        Assert.Equal([160], queue.ReadyWidths("key", 2000));

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.Equal([160, 400], queue.ReadyWidths("key", 2000));

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.False(queue.IsProcessing("key"));
        Assert.Equal(ImageVariants.WidthsFor(2000), queue.ReadyWidths("key", 2000));
    }

    // what an island's thumbnail waits for to show the sharp picture
    [Fact]
    public async Task SaysAnImageIsFinishedOnceItsLastJobIsDone()
    {
        var queue = new BackgroundImageSizeQueue();
        List<string> finished = [];
        queue.SizesFinished += finished.Add;
        queue.Enqueue(new ImageSizeJob(Storage, "key", ImageVariants.LaterWidths(2000)));

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.Empty(finished);

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["key"], finished);
    }
}
