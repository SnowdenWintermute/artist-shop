using ArtistShop.Web.Images;
using ArtistShop.Web.Tests.Images;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// What ImageSizeQueueWorker queues at startup, for sizes a restart lost
public sealed class MissingImageSizesTests(TestApp app)
{
    private const int ImageWidth = 1000;

    private MissingImageSizes MissingImageSizes => app.Services.GetRequiredService<MissingImageSizes>();

    [Fact]
    public async Task AnImageWithEverySizeNeedsNothing()
    {
        var (site, _, _) = await app.MakeSiteWithAWorkImageAsync(ImageWidth);

        Assert.Empty(MissingImageSizes.Find(site.Id));
    }

    [Fact]
    public async Task TheSizesARestartLostAreMadeAgain()
    {
        var (site, _, storageKey) = await app.MakeSiteWithAWorkImageAsync(ImageWidth);
        app.DeleteImageSize(site, storageKey, 400);
        app.DeleteImageSize(site, storageKey, 800);

        var job = Assert.Single(MissingImageSizes.Find(site.Id));
        new ImmediateImageSizeQueue().Enqueue(job);

        Assert.Equal(storageKey, job.StorageKey);
        Assert.Equal([400, 800], job.Widths);
        Assert.Equal(ImageVariants.WidthsFor(ImageWidth).Length * 2, app.ImageStorageOf(site).VariantFileNames(storageKey).Count);
    }

    // the restart came after the last size but before the mark was cleared
    [Fact]
    public async Task AMarkedImageWithEverySizeLosesItsMark()
    {
        var (site, _, storageKey) = await app.MakeSiteWithAWorkImageAsync(ImageWidth);
        var imageStorage = app.ImageStorageOf(site);
        imageStorage.MarkSizesPending(storageKey);

        Assert.Empty(MissingImageSizes.Find(site.Id));
        Assert.Empty(imageStorage.ListKeysWithSizesPending());
    }
}
