using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.Images;

public sealed class ImageProcessingLimiterTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    private static ImageProcessingLimiter CreateLimiter(
        int processorSlots,
        int memoryBudgetMegabytes,
        int queuedImageLimit
    ) =>
        new(
            new ImageProcessingCapacity(processorSlots, memoryBudgetMegabytes, queuedImageLimit),
            TestImageProcessing.Settings.BusyRetryAfter
        );

    // a task that isn't complete straight after the call is waiting in a queue
    [Fact]
    public async Task AnImageWaitsForAFreeSlot()
    {
        var limiter = CreateLimiter(processorSlots: 1, memoryBudgetMegabytes: 100, queuedImageLimit: 1);
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await limiter.AcquireAsync(10, cancellationToken);
        var second = limiter.AcquireAsync(10, cancellationToken);
        Assert.False(second.IsCompleted);

        first.Dispose();
        (await second.WaitAsync(WaitLimit, cancellationToken)).Dispose();
    }

    [Fact]
    public async Task TurnsAnImageAwayWhenTheQueueIsFull()
    {
        var limiter = CreateLimiter(processorSlots: 1, memoryBudgetMegabytes: 100, queuedImageLimit: 0);
        var cancellationToken = TestContext.Current.CancellationToken;

        using var first = await limiter.AcquireAsync(10, cancellationToken);

        var exception = await Assert.ThrowsAsync<ImageProcessingBusyException>(() =>
            limiter.AcquireAsync(10, cancellationToken)
        );
        Assert.Equal(TestImageProcessing.Settings.BusyRetryAfter, exception.RetryAfter);
    }

    [Fact]
    public async Task AnImageWaitsForMemoryEvenWithAFreeSlot()
    {
        var limiter = CreateLimiter(processorSlots: 2, memoryBudgetMegabytes: 100, queuedImageLimit: 0);
        var cancellationToken = TestContext.Current.CancellationToken;

        var large = await limiter.AcquireAsync(80, cancellationToken);
        var medium = limiter.AcquireAsync(40, cancellationToken);
        Assert.False(medium.IsCompleted);

        large.Dispose();
        (await medium.WaitAsync(WaitLimit, cancellationToken)).Dispose();
    }

    [Fact]
    public async Task ACancelledWaitGivesUpItsQueuePlace()
    {
        var limiter = CreateLimiter(processorSlots: 1, memoryBudgetMegabytes: 100, queuedImageLimit: 1);
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await limiter.AcquireAsync(10, cancellationToken);

        using var closedTab = new CancellationTokenSource();
        var abandoned = limiter.AcquireAsync(10, closedTab.Token);
        await closedTab.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);

        // the queue holds one image, so this would be turned away if the abandoned one still held the place
        var next = limiter.AcquireAsync(10, cancellationToken);
        Assert.False(next.IsCompleted);

        first.Dispose();
        (await next.WaitAsync(WaitLimit, cancellationToken)).Dispose();
    }

    [Fact]
    public async Task ACancelledWaitForMemoryHandsBackItsSlot()
    {
        var limiter = CreateLimiter(processorSlots: 2, memoryBudgetMegabytes: 100, queuedImageLimit: 0);
        var cancellationToken = TestContext.Current.CancellationToken;

        using var large = await limiter.AcquireAsync(80, cancellationToken);

        using var closedTab = new CancellationTokenSource();
        var abandoned = limiter.AcquireAsync(40, closedTab.Token);
        await closedTab.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);

        // both slots would be taken, and the queue is empty, if the abandoned image kept its slot
        using var small = await limiter.AcquireAsync(10, cancellationToken);
    }

    [Fact]
    public async Task RejectsMoreThanTheWholeMemoryBudget()
    {
        var limiter = CreateLimiter(processorSlots: 1, memoryBudgetMegabytes: 100, queuedImageLimit: 0);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            limiter.AcquireAsync(101, TestContext.Current.CancellationToken)
        );
    }
}
