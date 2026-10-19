using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Pages.Catalog;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// an image component's settings page, and what it shows visitors
public sealed class ImageComponentTests(TestApp app)
{
    private SiteDatabase DatabaseOf(TestSite site) => app.Services.GetRequiredService<SiteDatabases>().For(site.Id);

    private sealed record ImagePage(CustomPageId PageId, PageRowId RowId, CustomComponentId ImageId)
    {
        public string SettingsUrl => PageUrls.EditCustomComponent(ImageId, PageId, RowId);
    }

    // a page at /image whose one row holds a new image component
    private async Task<ImagePage> AddImagePageAsync(TestSite site)
    {
        var database = DatabaseOf(site);
        var pageId = await new CustomPageRepository(database).AddAsync("Image", new CustomPageAddress("image"));
        var rowId = await new PageRowRepository(database).AddAsync(pageId);
        var slot = Assert.Single(Assert.Single((await new CustomPageRepository(database).GetLayoutAsync(pageId)).Rows).Slots);
        var imageId = await new CustomComponentRepository(database).AddToSlotAsync(slot.Id, "Image 1", ImageSettings.New);

        return new ImagePage(pageId, rowId, imageId);
    }

    private async Task<CustomComponentSettings> SettingsOfAsync(TestSite site, CustomComponentId id) =>
        Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(DatabaseOf(site)).GetAsync(id)).Component.Settings;

    [Fact]
    public async Task TheSettingsPageSavesHeightFitPaddingAndLink()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            imagePage.SettingsUrl,
            "custom-component",
            new()
            {
                ["Input.Name"] = "Image 1",
                ["Input.ImageHeight"] = nameof(ImageHeight.Medium),
                ["Input.Fit"] = nameof(ImageFit.Contain),
                ["Input.Padding"] = nameof(ImagePadding.Large),
                ["Input.Link"] = nameof(ImageLink.None),
            }
        );

        Assert.Equal(new ImageSettings(null, ImageHeight.Medium, ImageFit.Contain, ImagePadding.Large, ImageLink.None), await SettingsOfAsync(site, imagePage.ImageId));
    }

    [Fact]
    public async Task ASelectLeftOutIsRefused()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            imagePage.SettingsUrl,
            "custom-component",
            new()
            {
                ["Input.Name"] = "Image 1",
                ["Input.ImageHeight"] = nameof(ImageHeight.Natural),
                ["Input.Fit"] = nameof(ImageFit.Contain),
                ["Input.Link"] = nameof(ImageLink.None),
            }
        );

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ImageSettings.New, await SettingsOfAsync(site, imagePage.ImageId));
    }

    private async Task<UploadedImage> AddLibraryImageAsync(TestSite site)
    {
        var library = new UploadedImageRepository(DatabaseOf(site));
        var storageKey = await UploadedImageTestData.AddAsync(library, "pier.jpg", "A pier at dusk");
        return (await library.GetByStorageKeysAsync([storageKey]))[storageKey];
    }

    private static Dictionary<string, string> FormChoosing(string storageKey) =>
        new()
        {
            ["Input.Name"] = "Image 1",
            ["Input.ImageStorageKey"] = storageKey,
            ["Input.ImageHeight"] = nameof(ImageHeight.Natural),
            ["Input.Fit"] = nameof(ImageFit.Cover),
            ["Input.Padding"] = nameof(ImagePadding.None),
            ["Input.Link"] = nameof(ImageLink.Lightbox),
        };

    [Fact]
    public async Task AChosenLibraryImageIsShownWithItsAltText()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var image = await AddLibraryImageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, imagePage.SettingsUrl, "custom-component", FormChoosing(image.StorageKey));

        Assert.Equal(ImageSettings.New with { Source = new ImageSource.Library(image.StorageKey) }, await SettingsOfAsync(site, imagePage.ImageId));
        var page = await app.ClientFor(site.Host).GetStringAsync("/image", TestContext.Current.CancellationToken);
        Assert.Contains($"alt=\"{image.AltText}\"", page);
        Assert.Contains("data-test=\"image-component\"", page);
    }

    [Fact]
    public async Task ANewImageComponentShowsNothing()
    {
        var site = await app.MakeSiteAsync();
        await AddImagePageAsync(site);

        var page = await app.ClientFor(site.Host).GetStringAsync("/image", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("data-test=\"image-component\"", page);
    }

    [Fact]
    public async Task AnImageNoLongerInTheLibraryIsRefused()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var image = await AddLibraryImageAsync(site);
        await new UploadedImageRepository(DatabaseOf(site)).DeleteAsync([image.StorageKey]);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(client, imagePage.SettingsUrl, "custom-component", FormChoosing(image.StorageKey));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ImageSettings.New, await SettingsOfAsync(site, imagePage.ImageId));
    }

    // a painting with two images, the first its main one
    // In a page-width row the image never asks for the full-bleed size. In an edge-to-edge row the first
    // visit queues it, which the tests' queue makes at once, and the next loads it; the settings page
    // then says a work's image is shown wider
    [Fact]
    public async Task AnImageInAnEdgeToEdgeRowLoadsTheFullBleedSizeAndSaysSo()
    {
        var workImage = await app.MakeSiteWithAWorkImageAsync(imageWidth: 2400);
        var site = workImage.Site;
        var work = Assert.IsType<Work>(await new WorkRepository(DatabaseOf(site)).GetBySlugAsync(workImage.WorkSlug.Value));
        var imagePage = await AddImagePageAsync(site);
        await new CustomComponentRepository(DatabaseOf(site)).UpdateAsync(
            imagePage.ImageId,
            "Image 1",
            ImageSettings.New with { Source = new ImageSource.Work(work.Id, StorageKey: null) }
        );
        var fullBleedSize = $"/{ImageVariants.FileName(ImageVariants.FullBleedWidth, ImageVariantFormat.Avif)} {ImageVariants.FullBleedWidth}w";
        var hint = $"data-hint=\"{HintType.FullBleedWorkImages}\"";
        var visitor = app.ClientFor(site.Host);
        var admin = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var cancellationToken = TestContext.Current.CancellationToken;

        await visitor.GetStringAsync("/image", cancellationToken);
        Assert.DoesNotContain(fullBleedSize, await visitor.GetStringAsync("/image", cancellationToken));
        Assert.DoesNotContain(hint, await admin.GetStringAsync(imagePage.SettingsUrl, cancellationToken));

        var row = Assert.Single((await new CustomPageRepository(DatabaseOf(site)).GetLayoutAsync(imagePage.PageId)).Rows);
        await new PageRowRepository(DatabaseOf(site)).SetSettingsAsync(row.Id, row.Settings with { ContentWidth = RowContentWidth.EdgeToEdge });

        await visitor.GetStringAsync("/image", cancellationToken);
        Assert.Contains(fullBleedSize, await visitor.GetStringAsync("/image", cancellationToken));
        Assert.Contains(hint, await admin.GetStringAsync(imagePage.SettingsUrl, cancellationToken));
    }

    private async Task<Work> AddPaintingAsync(TestSite site)
    {
        var identifiers = await new CatalogTestData(DatabaseOf(site)).AddPaintingAsync(
            $"Image test painting {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [],
            [CatalogTestData.CreateTestImage(), CatalogTestData.CreateTestImage()]
        );
        return Assert.IsType<Work>(await new WorkRepository(DatabaseOf(site)).GetByIdAsync(identifiers.Id));
    }

    private static Dictionary<string, string> FormChoosing(Work work, WorkImage image) =>
        new(FormChoosing(image.StorageKey)) { ["Input.ImageWorkId"] = work.Id.Value.ToString() };

    [Fact]
    public async Task AWorksMainImageFollowsTheWorkAndIsShownWithItsTitle()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var work = await AddPaintingAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, imagePage.SettingsUrl, "custom-component", FormChoosing(work, work.Images[0]));
        await new WorkRepository(DatabaseOf(site)).UpdateImagesAsync(work.Id, work.Images, mainImageIndex: 1);

        Assert.Equal(ImageSettings.New with { Source = new ImageSource.Work(work.Id, null) }, await SettingsOfAsync(site, imagePage.ImageId));
        var page = await app.ClientFor(site.Host).GetStringAsync("/image", TestContext.Current.CancellationToken);
        Assert.Contains($"alt=\"{work.Name.Value}\"", page);
        Assert.Contains(work.Images[1].StorageKey, page);
        Assert.DoesNotContain(work.Images[0].StorageKey, page);
        Assert.Contains(imagePage.PageId, (await new CustomPageRepository(DatabaseOf(site)).GetMentioningWorkAsync(work.Id)).Select(page => page.Id));
    }

    [Fact]
    public async Task AnotherImageOfAWorkStaysChosen()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var work = await AddPaintingAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, imagePage.SettingsUrl, "custom-component", FormChoosing(work, work.Images[1]));

        Assert.Equal(
            ImageSettings.New with { Source = new ImageSource.Work(work.Id, work.Images[1].StorageKey) },
            await SettingsOfAsync(site, imagePage.ImageId)
        );
    }

    // shown nowhere and mentioned nowhere, and the settings page can't be saved with it
    [Fact]
    public async Task AnImageTakenOffItsWorkIsNotShown()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var work = await AddPaintingAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        await TestApp.PostFormAsync(client, imagePage.SettingsUrl, "custom-component", FormChoosing(work, work.Images[1]));
        var saved = await SettingsOfAsync(site, imagePage.ImageId);

        await new WorkRepository(DatabaseOf(site)).UpdateImagesAsync(work.Id, [work.Images[0]], mainImageIndex: 0);
        var response = await TestApp.PostFormAsync(client, imagePage.SettingsUrl, "custom-component", FormChoosing(work, work.Images[1]));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(saved, await SettingsOfAsync(site, imagePage.ImageId));
        var page = await app.ClientFor(site.Host).GetStringAsync("/image", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("data-test=\"image-component\"", page);
        Assert.Empty(await new CustomPageRepository(DatabaseOf(site)).GetMentioningWorkAsync(work.Id));
    }

    // listed for the confirm dialog first, then gone from its page with the work
    [Fact]
    public async Task DeletingAWorkDeletesTheImageComponentsShowingIt()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var work = await AddPaintingAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        await TestApp.PostFormAsync(client, imagePage.SettingsUrl, "custom-component", FormChoosing(work, work.Images[1]));
        var components = new CustomComponentRepository(DatabaseOf(site));

        Assert.Equal([imagePage.ImageId], (await components.GetImageComponentsShowingWorksAsync([work.Id])).Select(component => component.Id));
        await new WorkRepository(DatabaseOf(site)).DeleteAsync(work.Id);

        Assert.Null(await components.GetAsync(imagePage.ImageId));
        Assert.Empty(Assert.Single((await new CustomPageRepository(DatabaseOf(site)).GetLayoutAsync(imagePage.PageId)).Rows).Slots.SelectMany(slot => slot.Stack));
    }

    private async Task<string> PublicPageAfterSavingAsync(TestSite site, ImagePage imagePage, Dictionary<string, string> form)
    {
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        await TestApp.PostFormAsync(client, imagePage.SettingsUrl, "custom-component", form);
        return await app.ClientFor(site.Host).GetStringAsync("/image", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnImageOpensTheLightboxByDefault()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var image = await AddLibraryImageAsync(site);

        var page = await PublicPageAfterSavingAsync(site, imagePage, FormChoosing(image.StorageKey));

        Assert.Contains("data-lightbox-open", page);
        Assert.Contains("<image-lightbox", page);
    }

    [Fact]
    public async Task AnImageLinkingNowhereHasNoLightbox()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var image = await AddLibraryImageAsync(site);

        var page = await PublicPageAfterSavingAsync(
            site,
            imagePage,
            new(FormChoosing(image.StorageKey)) { ["Input.Link"] = nameof(ImageLink.None) }
        );

        Assert.Contains("data-test=\"image-component\"", page);
        Assert.DoesNotContain("data-lightbox-open", page);
    }

    // the main image opens the work's page at its start, another image at that image
    [Theory]
    [InlineData(0, null)]
    [InlineData(1, 2)]
    public async Task AWorksImageCanLinkToItsPage(int imageIndex, int? imageNumber)
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var work = await AddPaintingAsync(site);

        var page = await PublicPageAfterSavingAsync(
            site,
            imagePage,
            new(FormChoosing(work, work.Images[imageIndex])) { ["Input.Link"] = nameof(ImageLink.ArtworkPage) }
        );

        Assert.Contains($"href=\"{WorkPageQuery.Url(work.Slug.Value, collectionSlug: null, imageNumber)}\"", page);
        Assert.DoesNotContain("data-lightbox-open", page);
    }

    [Fact]
    public async Task ALibraryImageLinkedToAnArtworkPageOpensTheLightbox()
    {
        var site = await app.MakeSiteAsync();
        var imagePage = await AddImagePageAsync(site);
        var image = await AddLibraryImageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            imagePage.SettingsUrl,
            "custom-component",
            new(FormChoosing(image.StorageKey)) { ["Input.Link"] = nameof(ImageLink.ArtworkPage) }
        );

        Assert.Equal(ImageLink.Lightbox, Assert.IsType<ImageSettings>(await SettingsOfAsync(site, imagePage.ImageId)).Link);
    }
}
