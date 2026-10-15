using ArtistShop.Web.Components.Pages.Catalog;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.App;

// While an image's larger sizes are still being made, pages show what's there and say so
public sealed class ImageProcessingLabelTests(TestApp app)
{
    private const int ImageWidth = 2000;

    private const string ProcessingLabel = "data-test=\"processing-label\"";

    [Fact]
    public async Task AWorkPageShowsOnlyTheReadySizesAndSaysTheRestAreProcessing()
    {
        var (site, workSlug, storageKey) = await app.MakeSiteWithAWorkImageAsync(ImageWidth);
        app.ImageSizeQueue.HoldBack(storageKey, 800, 1600);

        var page = await app.ClientFor(site.Host).GetStringAsync(WorkPageQuery.Url(workSlug.Value, collectionSlug: null, imageNumber: null), TestContext.Current.CancellationToken);

        Assert.Contains(ProcessingLabel, page);
        Assert.Contains($"/{storageKey}/{ImageVariants.FileName(400, ImageVariantFormat.Avif)}", page);
        Assert.DoesNotContain($"/{storageKey}/{ImageVariants.FileName(800, ImageVariantFormat.Avif)}", page);
        Assert.DoesNotContain($"/{storageKey}/{ImageVariants.FileName(1600, ImageVariantFormat.Avif)}", page);
    }

    [Fact]
    public async Task AWorkPageWithEverySizeSaysNothing()
    {
        var (site, workSlug, _) = await app.MakeSiteWithAWorkImageAsync(ImageWidth);

        var page = await app.ClientFor(site.Host).GetStringAsync(WorkPageQuery.Url(workSlug.Value, collectionSlug: null, imageNumber: null), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(ProcessingLabel, page);
    }
}
