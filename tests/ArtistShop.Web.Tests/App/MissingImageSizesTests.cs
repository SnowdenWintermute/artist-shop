using ArtistShop.Web.Images;
using ArtistShop.Web.Tests.Images;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// What ImageSizeQueueWorker queues at startup, for sizes a restart lost
public sealed class MissingImageSizesTests(TestApp app)
{
    private const int ImageWidth = 1000;

    [Fact]
    public async Task AnImageWithEverySizeNeedsNothing()
    {
        var (site, _, _) = await app.MakeSiteWithAWorkImageAsync(ImageWidth);

        Assert.Empty(await app.Services.GetRequiredService<MissingImageSizes>().FindAsync(site.Id));
    }

    [Fact]
    public async Task TheSizesARestartLostAreMadeAgain()
    {
        var (site, _, storageKey) = await app.MakeSiteWithAWorkImageAsync(ImageWidth);
        app.DeleteImageSize(site, storageKey, 400);
        app.DeleteImageSize(site, storageKey, 800);

        var job = Assert.Single(await app.Services.GetRequiredService<MissingImageSizes>().FindAsync(site.Id));
        new ImmediateImageSizeQueue().Enqueue(job);

        Assert.Equal(storageKey, job.StorageKey);
        Assert.Equal([400, 800], job.Widths);
        Assert.Equal(ImageVariants.WidthsFor(ImageWidth).Length * 2, app.ImageStorageOf(site).VariantFileNames(storageKey).Count);
    }
}
