using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Pages.Catalog;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// a carousel's settings page, its chosen slides, and what it shows visitors
public sealed class CarouselComponentTests(TestApp app)
{
    private SiteDatabase DatabaseOf(TestSite site) => app.Services.GetRequiredService<SiteDatabases>().For(site.Id);

    private sealed record CarouselPage(CustomPageId PageId, PageRowId RowId, CustomComponentId CarouselId)
    {
        public string SettingsUrl => PageUrls.EditCustomComponent(CarouselId, PageId, RowId);
    }

    // a page at /carousel whose one row holds a new carousel
    private async Task<CarouselPage> AddCarouselPageAsync(TestSite site)
    {
        var database = DatabaseOf(site);
        var pageId = await new CustomPageRepository(database).AddAsync("Carousel", new CustomPageAddress("carousel"));
        var rowId = await new PageRowRepository(database).AddAsync(pageId);
        var slot = Assert.Single(Assert.Single((await new CustomPageRepository(database).GetLayoutAsync(pageId)).Rows).Slots);
        var carouselId = await new CustomComponentRepository(database).AddToSlotAsync(slot.Id, "Carousel 1", CarouselSettings.New);

        return new CarouselPage(pageId, rowId, carouselId);
    }

    private async Task<CustomComponentSettings> SettingsOfAsync(TestSite site, CustomComponentId id) =>
        Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(DatabaseOf(site)).GetAsync(id)).Component.Settings;

    private static Dictionary<string, string> FormWith(CarouselSettings settings, int? collectionId) =>
        new()
        {
            ["Input.Name"] = "Carousel 1",
            ["Input.SlideSource"] = settings.Source.Type.ToString(),
            ["Input.CollectionId"] = collectionId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            ["Input.Transition"] = settings.Transition.ToString(),
            ["Input.Autoplay"] = settings.Autoplay.ToString(),
            ["Input.ImageHeight"] = settings.Height.ToString(),
            ["Input.Fit"] = settings.Fit.ToString(),
            ["Input.Controls"] = settings.Controls.ToString(),
            ["Input.CarouselLink"] = settings.Link.ToString(),
            ["Input.Caption"] = settings.Caption.ToString(),
        };

    private static Task<string> PublicPageAsync(TestApp app, TestSite site) =>
        app.ClientFor(site.Host).GetStringAsync("/carousel", TestContext.Current.CancellationToken);

    [Fact]
    public async Task TheSettingsPageSavesEveryChoice()
    {
        var site = await app.MakeSiteAsync();
        var carouselPage = await AddCarouselPageAsync(site);
        var collectionId = await new CatalogTestData(DatabaseOf(site)).AddCollectionAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var settings = new CarouselSettings(
            new SlideSource.WorksInCollection(collectionId),
            CarouselTransition.Slide,
            CarouselAutoplay.EightSeconds,
            ImageHeight.Fill,
            ImageFit.Contain,
            CarouselControls.Dots,
            CarouselLink.None,
            CarouselCaption.Title
        );

        await TestApp.PostFormAsync(client, carouselPage.SettingsUrl, "custom-component", FormWith(settings, collectionId.Value));

        Assert.Equal(settings, await SettingsOfAsync(site, carouselPage.CarouselId));
    }

    [Fact]
    public async Task TheImagesOwnHeightIsRefused()
    {
        var site = await app.MakeSiteAsync();
        var carouselPage = await AddCarouselPageAsync(site);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, carouselPage.SettingsUrl, "custom-component",
            FormWith(CarouselSettings.New with { Height = ImageHeight.Natural }, collectionId: null));

        Assert.Equal(CarouselSettings.New, await SettingsOfAsync(site, carouselPage.CarouselId));
    }

    [Fact]
    public async Task SlidesChosenWithThePickersAreShownInOrder()
    {
        var site = await app.MakeSiteAsync();
        var carouselPage = await AddCarouselPageAsync(site);
        var library = new UploadedImageRepository(DatabaseOf(site));
        var altText = $"Pier {Guid.NewGuid():n}";
        var storageKey = await UploadedImageTestData.AddAsync(library, "pier.jpg", altText);
        var workName = $"Harbour {Guid.NewGuid():n}";
        var work = await new CatalogTestData(DatabaseOf(site)).AddPaintingAsync(workName, [], [], [CatalogTestData.CreateTestImage()]);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, carouselPage.SettingsUrl, "add-slide", new() { ["Slide.ImageStorageKey"] = storageKey });
        await TestApp.PostFormAsync(client, carouselPage.SettingsUrl, "add-slide",
            new() { ["Slide.ImageStorageKey"] = "", ["Slide.ImageWorkId"] = work.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });

        var slides = await new SlideRepository(DatabaseOf(site)).GetAsync(new SlideOwner.Component(carouselPage.CarouselId));
        Assert.Equal([new ImageSource.Library(storageKey), new ImageSource.Work(work.Id, StorageKey: null)], slides.Select(slide => slide.Source));
        var page = await PublicPageAsync(app, site);
        Assert.True(page.IndexOf($"alt=\"{altText}\"", StringComparison.Ordinal) < page.IndexOf($"alt=\"{workName}\"", StringComparison.Ordinal));
        Assert.Contains($"href=\"{WorkPageQuery.Url(work.Slug.Value, collectionSlug: null, imageNumber: null)}\"", page);
    }

    [Fact]
    public async Task ACollectionsWorksWithImagesAreItsSlidesWithTheirTitles()
    {
        var site = await app.MakeSiteAsync();
        var carouselPage = await AddCarouselPageAsync(site);
        var catalog = new CatalogTestData(DatabaseOf(site));
        var collectionId = await catalog.AddCollectionAsync();
        var shownName = $"Shown {Guid.NewGuid():n}";
        await catalog.AddPaintingAsync(shownName, [], [collectionId], [CatalogTestData.CreateTestImage()]);
        await catalog.AddPaintingAsync($"Imageless {Guid.NewGuid():n}", [], [collectionId], []);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(client, carouselPage.SettingsUrl, "custom-component",
            FormWith(CarouselSettings.New with { Source = new SlideSource.WorksInCollection(collectionId), Caption = CarouselCaption.Title },
                collectionId.Value));

        var page = await PublicPageAsync(app, site);
        Assert.Single(page.Split("data-test=\"carousel-slide\"").Skip(1));
        Assert.Contains($"data-test=\"carousel-caption\">{shownName}<", page);
    }

    [Fact]
    public async Task ControlsComeWithASecondSlideAndPauseWithAutoplay()
    {
        var site = await app.MakeSiteAsync();
        var carouselPage = await AddCarouselPageAsync(site);
        var library = new UploadedImageRepository(DatabaseOf(site));
        var slides = new SlideRepository(DatabaseOf(site));
        var owner = new SlideOwner.Component(carouselPage.CarouselId);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        await slides.AddAsync(owner, new ImageSource.Library(await UploadedImageTestData.AddAsync(library, "pier.jpg", "Pier")));

        Assert.DoesNotContain("data-action=\"next\"", await PublicPageAsync(app, site));

        await slides.AddAsync(owner, new ImageSource.Library(await UploadedImageTestData.AddAsync(library, "dune.jpg", "Dune")));
        var still = await PublicPageAsync(app, site);
        Assert.Contains("data-action=\"next\"", still);
        Assert.Contains("data-part=\"dots\"", still);
        Assert.DoesNotContain("data-action=\"pause\"", still);

        await TestApp.PostFormAsync(client, carouselPage.SettingsUrl, "custom-component",
            FormWith(CarouselSettings.New with { Autoplay = CarouselAutoplay.FiveSeconds, Controls = CarouselControls.None }, collectionId: null));
        var playing = await PublicPageAsync(app, site);
        Assert.Contains("data-action=\"pause\"", playing);
        Assert.DoesNotContain("data-action=\"next\"", playing);
        Assert.DoesNotContain("data-part=\"dots\"", playing);
    }

    [Fact]
    public async Task ANewCarouselShowsNothing()
    {
        var site = await app.MakeSiteAsync();
        await AddCarouselPageAsync(site);

        Assert.DoesNotContain("data-test=\"carousel\"", await PublicPageAsync(app, site));
    }
}
