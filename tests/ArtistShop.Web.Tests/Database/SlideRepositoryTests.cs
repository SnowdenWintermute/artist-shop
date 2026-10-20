using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Tests.Database;

// chosen slides, owned by a row of a page of each test's own; a carousel's come with the Carousel type
[Collection(DatabaseCollection.Name)]
public sealed class SlideRepositoryTests(TestDatabaseFixture database)
{
    private readonly CustomPageRepository _pages = new(database.Site);
    private readonly PageRowRepository _rows = new(database.Site);
    private readonly SlideRepository _slides = new(database.Site);
    private readonly UploadedImageRepository _library = new(database.Site);
    private readonly CustomComponentRepository _components = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    private static string UniqueName() => $"Slide test {Guid.NewGuid():n}";

    private async Task<(CustomPageId PageId, string PageName, SlideOwner.Row Owner)> AddRowAsync()
    {
        var name = UniqueName();
        var pageId = await _pages.AddAsync(name, CustomPageAddress.FromName(name));

        return (pageId, name, new SlideOwner.Row(await _rows.AddAsync(pageId)));
    }

    // the row's background a slideshow showing the source
    private async Task ShowSlideshowAsync(CustomPageId pageId, SlideOwner.Row owner, SlideSource source)
    {
        var row = Unwrap.Value((await _pages.GetLayoutAsync(pageId)).Row(owner.Id));
        await _rows.SetSettingsAsync(owner.Id, row.Settings with
        {
            Background = new RowBackground.Slideshow(source, SlideshowInterval.TenSeconds, ImageFit.Cover, Behind: null),
        });
    }

    private Task<string> AddLibraryImageAsync(string altText) => UploadedImageTestData.AddAsync(_library, "pier.jpg", altText);

    // a work with two images, the first its main one
    private async Task<(WorkIdentifiers Work, WorkImage First, WorkImage Second)> AddWorkAsync(string name)
    {
        var first = CatalogTestData.CreateTestImage();
        var second = CatalogTestData.CreateTestImage();

        return (await _catalog.AddPaintingAsync(name, [], [], [first, second]), first, second);
    }

    [Fact]
    public async Task SlidesShowTheirImagesInTheOrderAdded()
    {
        var (_, _, owner) = await AddRowAsync();
        var altText = UniqueName();
        var storageKey = await AddLibraryImageAsync(altText);
        var workName = UniqueName();
        var (work, first, second) = await AddWorkAsync(workName);

        await _slides.AddAsync(owner, new ImageSource.Library(storageKey));
        await _slides.AddAsync(owner, new ImageSource.Work(work.Id, StorageKey: null));
        await _slides.AddAsync(owner, new ImageSource.Work(work.Id, second.StorageKey));
        // the same image twice
        await _slides.AddAsync(owner, new ImageSource.Library(storageKey));

        var slides = await _slides.GetAsync(owner);

        Assert.Equal(
            [
                new ImageSource.Library(storageKey),
                new ImageSource.Work(work.Id, null),
                new ImageSource.Work(work.Id, second.StorageKey),
                new ImageSource.Library(storageKey),
            ],
            slides.Select(slide => slide.Source)
        );
        var library = Assert.IsType<SlideImage.OfLibrary>(slides[0].Image);
        Assert.Equal((storageKey, altText, altText), (library.Image.StorageKey, library.AltText, library.Name));
        var main = Assert.IsType<SlideImage.OfWork>(slides[1].Image);
        Assert.Equal((first.StorageKey, workName, work.Slug, 0, true), (main.Image.StorageKey, main.WorkName.Value, main.WorkSlug, main.Place, main.IsMain));
        var picked = Assert.IsType<SlideImage.OfWork>(slides[2].Image);
        Assert.Equal((second.StorageKey, 1, false), (picked.Image.StorageKey, picked.Place, picked.IsMain));
    }

    [Fact]
    public async Task AnImageTakenOffItsWorkLeavesItsSlideWithNothingToShow()
    {
        var (_, _, owner) = await AddRowAsync();
        var (work, first, second) = await AddWorkAsync(UniqueName());
        await _slides.AddAsync(owner, new ImageSource.Work(work.Id, second.StorageKey));

        await new WorkRepository(database.Site).UpdateImagesAsync(work.Id, [first], mainImageIndex: 0);

        Assert.Null(Assert.Single(await _slides.GetAsync(owner)).Image);
    }

    [Fact]
    public async Task SlidesReorderAndRemoveAndAStaleOrderIsRefused()
    {
        var (_, _, owner) = await AddRowAsync();
        var keys = new[] { await AddLibraryImageAsync(UniqueName()), await AddLibraryImageAsync(UniqueName()), await AddLibraryImageAsync(UniqueName()) };

        foreach (var key in keys)
        {
            await _slides.AddAsync(owner, new ImageSource.Library(key));
        }

        var ids = (await _slides.GetAsync(owner)).Select(slide => slide.Id).ToList();

        await _slides.ReorderAsync(owner, [ids[2], ids[0], ids[1]]);
        Assert.Equal([ids[2], ids[0], ids[1]], (await _slides.GetAsync(owner)).Select(slide => slide.Id));

        await _slides.RemoveAsync(owner, ids[0]);
        Assert.Equal([ids[2], ids[1]], (await _slides.GetAsync(owner)).Select(slide => slide.Id));
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _slides.ReorderAsync(owner, [ids[1], ids[2], ids[0]]));
    }

    [Fact]
    public async Task ARemovedRowADeletedImageOrANonCarouselRefuseASlide()
    {
        var (_, _, owner) = await AddRowAsync();
        var storageKey = await AddLibraryImageAsync(UniqueName());
        await _library.DeleteAsync([storageKey]);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _slides.AddAsync(owner, new ImageSource.Library(storageKey)));

        var liveKey = await AddLibraryImageAsync(UniqueName());
        await _rows.DeleteAsync(owner.Id);
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _slides.AddAsync(owner, new ImageSource.Library(liveKey)));

        var (pageId, _, other) = await AddRowAsync();
        var slot = Assert.Single(Unwrap.Value((await _pages.GetLayoutAsync(pageId)).Row(other.Id)).Slots);
        var imageComponent = await _components.AddToSlotAsync(slot.Id, UniqueName(), ImageSettings.New);
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _slides.AddAsync(new SlideOwner.Component(imageComponent), new ImageSource.Library(liveKey)));
    }

    [Fact]
    public async Task DeletingAnImageOrWorkTakesItsSlidesAndTheWarningsSaySo()
    {
        var (pageId, pageName, owner) = await AddRowAsync();
        await ShowSlideshowAsync(pageId, owner, new SlideSource.ChosenSlides());
        var altText = UniqueName();
        var storageKey = await AddLibraryImageAsync(altText);
        var (work, _, _) = await AddWorkAsync(UniqueName());
        var kept = await AddLibraryImageAsync(UniqueName());
        await _slides.AddAsync(owner, new ImageSource.Library(storageKey));
        await _slides.AddAsync(owner, new ImageSource.Library(storageKey));
        await _slides.AddAsync(owner, new ImageSource.Work(work.Id, StorageKey: null));
        await _slides.AddAsync(owner, new ImageSource.Library(kept));

        // a slideshow holding it twice is one place, and one page
        Assert.Equal(1, Assert.Single((await _library.GetPageAsync(altText, pageNumber: 1)).Items).UsedInCount);
        var owners = await _slides.GetOwnersShowingImagesAsync([storageKey]);
        Assert.Empty(owners.Carousels);
        Assert.Equal([pageName], owners.SlideshowPages);
        Assert.Equal([pageName], (await _slides.GetOwnersShowingWorksAsync([work.Id])).SlideshowPages);

        await _library.DeleteAsync([storageKey]);
        await new WorkRepository(database.Site).DeleteAsync(work.Id);

        Assert.Equal([new ImageSource.Library(kept)], (await _slides.GetAsync(owner)).Select(slide => slide.Source));
    }

    [Fact]
    public async Task SlidesKeptWhileTheirOwnerShowsSomethingElseAreNotUses()
    {
        var (pageId, _, row) = await AddRowAsync();
        var altText = UniqueName();
        var storageKey = await AddLibraryImageAsync(altText);
        var slot = Assert.Single(Unwrap.Value((await _pages.GetLayoutAsync(pageId)).Row(row.Id)).Slots);
        var carouselName = UniqueName();
        var carousel = await _components.AddToSlotAsync(slot.Id, carouselName, CarouselSettings.New);
        await _slides.AddAsync(row, new ImageSource.Library(storageKey));
        await _slides.AddAsync(new SlideOwner.Component(carousel), new ImageSource.Library(storageKey));

        // the row has no background yet, while the new carousel shows its chosen slides
        Assert.Equal(1, Assert.Single((await _library.GetPageAsync(altText, pageNumber: 1)).Items).UsedInCount);
        var owners = await _slides.GetOwnersShowingImagesAsync([storageKey]);
        Assert.Equal([carouselName], owners.Carousels.Select(owner => owner.Name));
        Assert.Empty(owners.SlideshowPages);

        var collection = new SlideSource.WorksInCollection(await _catalog.AddCollectionAsync());
        await ShowSlideshowAsync(pageId, row, collection);
        await _components.UpdateAsync(carousel, carouselName, CarouselSettings.New with { Source = collection });

        Assert.Equal(0, Assert.Single((await _library.GetPageAsync(altText, pageNumber: 1)).Items).UsedInCount);
        owners = await _slides.GetOwnersShowingImagesAsync([storageKey]);
        Assert.Empty(owners.Carousels);
        Assert.Empty(owners.SlideshowPages);
    }
}
