using ArtistShop.Web.Images;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// The worker the app runs, which TestApp leaves out. Run alone, since its startup pass looks at every
// site's images and would remake sizes other tests leave out on purpose
[Collection(RunAloneCollection.Name)]
public sealed class ImageSizeQueueWorkerTests(TestApp app)
{
    private const int ImageWidth = 1000;

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    private static async Task WaitForSizesAsync(ImageStorage imageStorage, string storageKey)
    {
        var expectedCount = ImageVariants.WidthsFor(ImageWidth).Length * 2;
        var giveUpAt = DateTime.UtcNow + WaitLimit;

        while (imageStorage.VariantFileNames(storageKey).Count < expectedCount)
        {
            Assert.True(DateTime.UtcNow < giveUpAt, $"The sizes of {storageKey} weren't made within {WaitLimit}.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task RemakesLostSizesAtStartupAndThenMakesQueuedOnes()
    {
        var (lostSite, _, lostKey) = await app.MakeSiteWithAWorkImageAsync(ImageWidth);
        app.DeleteImageSize(lostSite, lostKey, 400);
        app.DeleteImageSize(lostSite, lostKey, 800);

        var (queuedSite, _, queuedKey) = await app.MakeSiteWithAWorkImageAsync(ImageWidth);
        app.DeleteImageSize(queuedSite, queuedKey, 800);

        var queue = new BackgroundImageSizeQueue();
        using var worker = ActivatorUtilities.CreateInstance<ImageSizeQueueWorker>(app.Services, queue);
        await worker.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            await WaitForSizesAsync(app.ImageStorageOf(lostSite), lostKey);

            // the startup pass found this one too; deleting the size again shows the queue at work
            await WaitForSizesAsync(app.ImageStorageOf(queuedSite), queuedKey);
            app.DeleteImageSize(queuedSite, queuedKey, 800);
            queue.Enqueue(new ImageSizeJob(app.ImageStorageOf(queuedSite), queuedKey, [800]));

            await WaitForSizesAsync(app.ImageStorageOf(queuedSite), queuedKey);
        }
        finally
        {
            await worker.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
