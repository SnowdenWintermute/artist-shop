using ArtistShop.Web.Images;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtistShop.Web.Tests.Images;

public sealed class BackgroundImageSizeQueueTests : IDisposable
{
    // where the queue marks the images it holds
    private readonly DirectoryInfo _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");

    private readonly ImageStorage _storage;

    public BackgroundImageSizeQueueTests() => _storage = new ImageStorage(_storageRoot.FullName);

    public void Dispose() => _storageRoot.Delete(recursive: true);

    private static BackgroundImageSizeQueue NewQueue() => new(NullLogger<BackgroundImageSizeQueue>.Instance);

    private static string NewKey() => ImageStorage.NewStorageKey();

    [Fact]
    public async Task EveryImagesSmallSizesComeBeforeAnyLargerOnes()
    {
        var queue = NewQueue();
        var first = NewKey();
        var second = NewKey();
        queue.Enqueue(new ImageSizeJob(_storage, first, [400, 800, 1600]));
        queue.Enqueue(new ImageSizeJob(_storage, second, [400, 800, 1600]));

        List<string> taken = [];
        for (var i = 0; i < 4; i++)
        {
            var job = await queue.TakeAsync(TestContext.Current.CancellationToken);
            taken.Add($"{job.StorageKey} {string.Join(" ", job.Widths)}");
        }

        Assert.Equal([$"{first} 400", $"{second} 400", $"{first} 800 1600", $"{second} 800 1600"], taken);
    }

    [Fact]
    public async Task ATakerWaitsForAJob()
    {
        var queue = NewQueue();
        var key = NewKey();

        var taking = queue.TakeAsync(TestContext.Current.CancellationToken);
        Assert.False(taking.IsCompleted);

        queue.Enqueue(new ImageSizeJob(_storage, key, [800]));

        Assert.Equal(key, (await taking).StorageKey);
    }

    // an image narrower than the smallest later width has nothing left to queue
    [Fact]
    public async Task NoWidthsQueueNothing()
    {
        var queue = NewQueue();
        var nothing = NewKey();
        var something = NewKey();
        queue.Enqueue(new ImageSizeJob(_storage, nothing, []));
        queue.Enqueue(new ImageSizeJob(_storage, something, [800]));

        Assert.Equal(something, (await queue.TakeAsync(TestContext.Current.CancellationToken)).StorageKey);
        Assert.Equal([something], _storage.ListKeysWithSizesPending());
    }

    [Fact]
    public async Task AnImagesSizesAreReadyOnceItsJobsAreDone()
    {
        var queue = NewQueue();
        var key = NewKey();
        queue.Enqueue(new ImageSizeJob(_storage, key, ImageVariants.LaterWidths(2000)));

        Assert.True(queue.IsProcessing(key, ImageVariants.MainImageWidth));
        Assert.Equal([160], queue.ReadyWidths(key, 2000));

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.Equal([160, 400], queue.ReadyWidths(key, 2000));

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.False(queue.IsProcessing(key, ImageVariants.MainImageWidth));
        Assert.Equal(ImageVariants.WidthsFor(2000), queue.ReadyWidths(key, 2000));
    }

    // a thumbnail already has the only size it uses, so it isn't labelled while larger ones are made
    [Fact]
    public void APictureIsProcessingOnlyWhileASizeItUsesIsPending()
    {
        var queue = NewQueue();
        var key = NewKey();
        queue.Enqueue(new ImageSizeJob(_storage, key, ImageVariants.LaterWidths(2000)));

        Assert.False(queue.IsProcessing(key, ImageVariants.AdminThumbnailWidth));
        Assert.True(queue.IsProcessing(key, ImageVariants.AdminPreviewWidth));
    }

    // what lets a restart find the image, until its last job is done
    [Fact]
    public async Task AnImageIsMarkedInItsStorageUntilItsLastJobIsDone()
    {
        var queue = NewQueue();
        var key = NewKey();
        queue.Enqueue(new ImageSizeJob(_storage, key, ImageVariants.LaterWidths(2000)));
        Assert.Equal([key], _storage.ListKeysWithSizesPending());

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.Equal([key], _storage.ListKeysWithSizesPending());

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_storage.ListKeysWithSizesPending());
    }

    // what an island's thumbnail waits for to show the sharp picture
    [Fact]
    public async Task SaysAnImageIsFinishedOnceItsLastJobIsDone()
    {
        var queue = NewQueue();
        var key = NewKey();
        List<string> finished = [];
        queue.SizesFinished += finished.Add;
        queue.Enqueue(new ImageSizeJob(_storage, key, ImageVariants.LaterWidths(2000)));

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.Empty(finished);

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));
        Assert.Equal([key], finished);
    }

    [Fact]
    public async Task AListenerThatThrowsDoesntKeepTheNewsFromTheRest()
    {
        var queue = NewQueue();
        var key = NewKey();
        List<string> finished = [];
        queue.SizesFinished += _ => throw new InvalidOperationException("A broken listener.");
        queue.SizesFinished += finished.Add;
        queue.Enqueue(new ImageSizeJob(_storage, key, [800]));

        queue.MarkDone(await queue.TakeAsync(TestContext.Current.CancellationToken));

        Assert.Equal([key], finished);
    }
}
