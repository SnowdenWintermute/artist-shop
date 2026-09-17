using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.Images;

public sealed class ImageProcessingCapacityTests
{
    private static ImageProcessingCapacity For(int processorCount, long heapMemoryMegabytes) =>
        ImageProcessingCapacity.For(TestImageProcessing.Settings, processorCount, heapMemoryMegabytes);

    [Fact]
    public void LeavesACoreForServingPages()
    {
        var capacity = For(processorCount: 4, heapMemoryMegabytes: 4096);

        Assert.Equal(3, capacity.ProcessorSlots);
    }

    [Fact]
    public void AOneCoreHostStillGetsASlot()
    {
        var capacity = For(processorCount: 1, heapMemoryMegabytes: 4096);

        Assert.Equal(1, capacity.ProcessorSlots);
    }

    [Fact]
    public void TheQueueHoldsWhatTheSlotsCanFinishBeforeTheProxyGivesUp()
    {
        // the settings allow a minute and estimate 10 seconds an image, so each slot works through
        // six waiting images in that minute
        var capacity = For(processorCount: 4, heapMemoryMegabytes: 4096);

        Assert.Equal(18, capacity.QueuedImageLimit);
    }

    [Fact]
    public void RefusesABudgetLargerThanTheMemoryDotNetMayUse()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            For(processorCount: 4, heapMemoryMegabytes: TestImageProcessing.Settings.MemoryBudgetMegabytes)
        );

        Assert.Contains(TestImageProcessing.Settings.MemoryBudgetMegabytes.ToString(), exception.Message);
    }

    [Fact]
    public void KeepsTheBudgetItWasGiven()
    {
        var capacity = For(processorCount: 4, heapMemoryMegabytes: 4096);

        Assert.Equal(TestImageProcessing.Settings.MemoryBudgetMegabytes, capacity.MemoryBudgetMegabytes);
    }
}
