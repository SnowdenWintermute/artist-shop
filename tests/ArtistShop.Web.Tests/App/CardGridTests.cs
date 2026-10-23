using System.Globalization;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// what a card grid on a custom page shows visitors, and its settings page
public sealed partial class CardGridTests(TestApp app)
{
    private sealed record GridPage(CustomPageId PageId, PageRowId RowId, IReadOnlyList<CustomComponentId> Grids)
    {
        public string SettingsUrl => PageUrls.EditCustomComponent(Grids[0], PageId, RowId);
    }

    // a page at /grid whose one row holds the grids side by side
    private async Task<GridPage> AddGridPageAsync(TestSite site, params CardGridSettings[] grids)
    {
        var database = app.DatabaseOf(site);
        var pageId = await new CustomPageRepository(database).AddAsync("Grid", new CustomPageAddress("grid"));
        var rowId = await new PageRowRepository(database).AddAsync(pageId);

        if (grids.Length > 1)
        {
            await new RowSlotRepository(database).AddAsync(rowId, slotCount: 1);
        }

        var slots = Assert.Single((await new CustomPageRepository(database).GetLayoutAsync(pageId)).Rows).Slots;
        var components = new CustomComponentRepository(database);
        List<CustomComponentId> ids = [];

        for (var index = 0; index < grids.Length; index++)
        {
            ids.Add(await components.AddToSlotAsync(slots[index].Id, $"Grid {index}", grids[index]));
        }

        return new GridPage(pageId, rowId, ids);
    }

    private static async Task<CollectionSlug> SlugOfAsync(CollectionRepository collections, CollectionId id) =>
        Assert.IsType<Collection>(await collections.GetByIdAsync(id)).Slug;

    // the cards' links, in the order the page draws them
    private static List<string> CardLinks(string page) =>
        [.. CardLink().Matches(page).Select(match => match.Groups["href"].Value)];

    // the page numbers the grid's page links go to, Previous and Next among them, in order
    private static List<int> LinkedPageNumbers(string page, string key)
    {
        var nav = PageLinksNav().Match(page).Value;
        return
        [
            .. Regex.Matches(nav, $"href=\"[^\"]*?(?:\\?{key}=(?<number>\\d+))?\"")
                .Select(match => match.Groups["number"].Success ? int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture) : 1)
                .Distinct()
                .Order(),
        ];
    }

    [Fact]
    public async Task OneCollectionsWorksAreHeadedByItsNameAndLinkWithinIt()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var collectionId = await catalog.AddCollectionAsync();
        var work = await catalog.AddPaintingInCollectionAsync(collectionId, [CatalogTestData.CreateTestImage()]);
        var collection = Assert.IsType<Collection>(await new CollectionRepository(app.DatabaseOf(site)).GetByIdAsync(collectionId));
        await AddGridPageAsync(site, CardGridSettings.AllCollections with
        {
            Source = new CardGridSource.WorksInCollection(collectionId),
        });

        var page = await app.ClientFor(site.Host).GetStringAsync("/grid", TestContext.Current.CancellationToken);

        Assert.Contains($"<h1 class=\"mb-4\">{collection.Name.Value}</h1>", page);
        Assert.Equal([$"/works/{work.Slug.Value}?collection={collection.Slug.Value}"], CardLinks(page));
    }

    [Fact]
    public async Task CollectionsComeBeforeLooseWorksNewestFirstAndOnlyThoseWithImages()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var collectionId = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(collectionId, [CatalogTestData.CreateTestImage()]);
        var older = await catalog.AddPaintingAsync("Older", [], [], [CatalogTestData.CreateTestImage()]);
        var newer = await catalog.AddPaintingAsync("Newer", [], [], [CatalogTestData.CreateTestImage()]);
        await catalog.AddPaintingAsync("No image", [], [], []);
        var collectionSlug = await SlugOfAsync(new CollectionRepository(app.DatabaseOf(site)), collectionId);
        await AddGridPageAsync(site, CardGridSettings.AllCollections with
        {
            Source = new CardGridSource.CollectionsAndLooseWorks(),
            Heading = new CardGridHeading.None(),
        });

        var page = await app.ClientFor(site.Host).GetStringAsync("/grid", TestContext.Current.CancellationToken);

        Assert.Equal(
            [PageUrls.Collection(collectionSlug), $"/works/{newer.Slug.Value}", $"/works/{older.Slug.Value}"],
            CardLinks(page)
        );
    }

    [Fact]
    public async Task LooseWorksPageOnFromWhereTheCollectionsEnd()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var collections = new CollectionRepository(app.DatabaseOf(site));
        var first = await catalog.AddCollectionAsync();
        var second = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(first, [CatalogTestData.CreateTestImage()]);
        await catalog.AddPaintingInCollectionAsync(second, [CatalogTestData.CreateTestImage()]);
        var oldest = await catalog.AddPaintingAsync("Oldest", [], [], [CatalogTestData.CreateTestImage()]);
        var middle = await catalog.AddPaintingAsync("Middle", [], [], [CatalogTestData.CreateTestImage()]);
        var newest = await catalog.AddPaintingAsync("Newest", [], [], [CatalogTestData.CreateTestImage()]);
        var gridPage = await AddGridPageAsync(site, CardGridSettings.AllCollections with
        {
            Source = new CardGridSource.CollectionsAndLooseWorks(),
            CardsPerPage = 2,
        });
        var key = $"page-{gridPage.Grids[0].Value}";
        var visitor = app.ClientFor(site.Host);

        var pageOne = await visitor.GetStringAsync("/grid", TestContext.Current.CancellationToken);
        var pageTwo = await visitor.GetStringAsync($"/grid?{key}=2", TestContext.Current.CancellationToken);
        var pageThree = await visitor.GetStringAsync($"/grid?{key}=3", TestContext.Current.CancellationToken);

        Assert.Equal(
            [PageUrls.Collection(await SlugOfAsync(collections, first)), PageUrls.Collection(await SlugOfAsync(collections, second))],
            CardLinks(pageOne)
        );
        // the collections fill the first page, and the works after them still count towards its page links
        Assert.Equal([2, 3], LinkedPageNumbers(pageOne, key));
        Assert.Equal([$"/works/{newest.Slug.Value}", $"/works/{middle.Slug.Value}"], CardLinks(pageTwo));
        Assert.Equal([$"/works/{oldest.Slug.Value}"], CardLinks(pageThree));
    }

    [Fact]
    public async Task PageLinksNumberTheEndsAndTheCurrentPagesNeighbours()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        for (var index = 0; index < 10; index++)
        {
            await catalog.AddPaintingInCollectionAsync(await catalog.AddCollectionAsync(), [CatalogTestData.CreateTestImage()]);
        }

        var gridPage = await AddGridPageAsync(site, CardGridSettings.AllCollections with { CardsPerPage = 1 });
        var key = $"page-{gridPage.Grids[0].Value}";

        var page = await app.ClientFor(site.Host).GetStringAsync($"/grid?{key}=6", TestContext.Current.CancellationToken);

        // 1 … 4 5 6 7 8 9 10: two and three are skipped, and nine shows as the only page between eight and ten
        Assert.Matches(CurrentPage(6), page);
        Assert.Equal([1, 4, 5, 7, 8, 9, 10], LinkedPageNumbers(page, key));
    }

    [Fact]
    public async Task TwoGridsOnOnePageTurnTheirPagesSeparately()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var collections = new CollectionRepository(app.DatabaseOf(site));
        var first = await catalog.AddCollectionAsync();
        var second = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(first, [CatalogTestData.CreateTestImage()]);
        await catalog.AddPaintingInCollectionAsync(second, [CatalogTestData.CreateTestImage()]);
        var onePerPage = CardGridSettings.AllCollections with { CardsPerPage = 1 };
        var gridPage = await AddGridPageAsync(site, onePerPage, onePerPage);

        var page = await app.ClientFor(site.Host)
            .GetStringAsync($"/grid?page-{gridPage.Grids[0].Value}=2", TestContext.Current.CancellationToken);

        Assert.Equal(
            [PageUrls.Collection(await SlugOfAsync(collections, second)), PageUrls.Collection(await SlugOfAsync(collections, first))],
            CardLinks(page)
        );
        // the second grid's next page keeps the first grid where it is
        Assert.Contains($"/grid?page-{gridPage.Grids[0].Value}=2&amp;page-{gridPage.Grids[1].Value}=2", page);
    }

    [Fact]
    public async Task WithoutPageLinksAGridShowsOnlyItsFirstPage()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var first = await catalog.AddCollectionAsync();
        var second = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(first, [CatalogTestData.CreateTestImage()]);
        await catalog.AddPaintingInCollectionAsync(second, [CatalogTestData.CreateTestImage()]);
        var gridPage = await AddGridPageAsync(site, CardGridSettings.AllCollections with { CardsPerPage = 1, HasPageLinks = false });

        var page = await app.ClientFor(site.Host)
            .GetStringAsync($"/grid?page-{gridPage.Grids[0].Value}=2", TestContext.Current.CancellationToken);

        Assert.Equal([PageUrls.Collection(await SlugOfAsync(new CollectionRepository(app.DatabaseOf(site)), first))], CardLinks(page));
        Assert.DoesNotMatch(PageLinksNav(), page);
    }

    [Fact]
    public async Task TheSettingsPageSavesASourceAndACustomHeading()
    {
        var site = await app.MakeSiteAsync();
        var collectionId = await new CatalogTestData(app.DatabaseOf(site)).AddCollectionAsync();
        var gridPage = await AddGridPageAsync(site, CardGridSettings.AllCollections);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            gridPage.SettingsUrl,
            "custom-component",
            new()
            {
                ["Input.Name"] = "Grid 0",
                ["Input.Source"] = nameof(CardGridSourceType.WorksInCollection),
                ["Input.CollectionId"] = collectionId.Value.ToString(),
                ["Input.HeadingType"] = nameof(CardGridHeadingType.Custom),
                ["Input.HeadingText"] = " Recent work ",
                ["Input.CardsPerPage"] = "3",
                ["Input.Layout"] = nameof(CardGridLayout.Masonry),
            }
        );

        var saved = Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(app.DatabaseOf(site)).GetAsync(gridPage.Grids[0]));
        Assert.Equal(
            new CardGridSettings(
                new CardGridSource.WorksInCollection(collectionId),
                new CardGridHeading.Custom("Recent work"),
                3,
                HasPageLinks: false,
                CardGridLayout.Masonry
            ),
            saved.Component.Settings
        );
    }

    [Fact]
    public async Task OneCollectionsWorksNeedTheCollectionChosen()
    {
        var site = await app.MakeSiteAsync();
        var gridPage = await AddGridPageAsync(site, CardGridSettings.AllCollections);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            gridPage.SettingsUrl,
            "custom-component",
            new()
            {
                ["Input.Name"] = "Grid 0",
                ["Input.Source"] = nameof(CardGridSourceType.WorksInCollection),
                ["Input.HeadingType"] = nameof(CardGridHeadingType.None),
                ["Input.CardsPerPage"] = "24",
                ["Input.Layout"] = nameof(CardGridLayout.EvenGrid),
            }
        );

        Assert.Contains("Choose one.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var saved = Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(app.DatabaseOf(site)).GetAsync(gridPage.Grids[0]));
        Assert.Equal(CardGridSettings.AllCollections, saved.Component.Settings);
    }

    [Fact]
    public async Task AMixIsHeadedByTheWorksWord()
    {
        var site = await app.MakeSiteAsync();
        await AddGridPageAsync(site, CardGridSettings.AllCollections with { Source = new CardGridSource.CollectionsAndLooseWorks() });

        var page = await app.ClientFor(site.Host).GetStringAsync("/grid", TestContext.Current.CancellationToken);

        Assert.Contains($"<h1 class=\"mb-4\">{SiteWording.Default.Work.PluralHeading}</h1>", page);
    }

    [Theory]
    [InlineData(CardGridLayout.EvenGrid, 1)]
    [InlineData(CardGridLayout.Rows, 0)]
    [InlineData(CardGridLayout.Masonry, 0)]
    public async Task AnEvenGridFillsAShortLastPage(CardGridLayout layout, int placeholderCount)
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));

        for (var index = 0; index < 3; index++)
        {
            await catalog.AddPaintingInCollectionAsync(await catalog.AddCollectionAsync(), [CatalogTestData.CreateTestImage()]);
        }

        var gridPage = await AddGridPageAsync(site,
            CardGridSettings.AllCollections with { CardsPerPage = 2, Layout = layout });
        var visitor = app.ClientFor(site.Host);

        var first = await visitor.GetStringAsync("/grid", TestContext.Current.CancellationToken);
        var last = await visitor.GetStringAsync($"/grid?page-{gridPage.Grids[0].Value}=2", TestContext.Current.CancellationToken);

        Assert.Equal(0, Placeholder().Count(first));
        Assert.Single(CardLinks(last));
        Assert.Equal(placeholderCount, Placeholder().Count(last));
        // an empty title line has no height, which made a placeholder shorter than a card
        Assert.DoesNotContain("title=\"\"></span>", last);
    }

    [Theory]
    [InlineData(CardGridLayout.EvenGrid, false)]
    [InlineData(CardGridLayout.Rows, true)]
    [InlineData(CardGridLayout.Masonry, true)]
    public async Task CardsKeepTheirPicturesShapeOnlyOutsideAnEvenGrid(CardGridLayout layout, bool isUncropped)
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        await catalog.AddPaintingInCollectionAsync(await catalog.AddCollectionAsync(), [CatalogTestData.CreateTestImage()]);
        await catalog.AddPaintingAsync("Loose", [], [], [CatalogTestData.CreateTestImage()]);
        await AddGridPageAsync(site, CardGridSettings.AllCollections with
        {
            Source = new CardGridSource.CollectionsAndLooseWorks(),
            Layout = layout,
        });

        var page = await app.ClientFor(site.Host).GetStringAsync("/grid", TestContext.Current.CancellationToken);

        // the collection's cover and the loose work, both 800 by 600 test images
        Assert.Equal(isUncropped ? 2 : 0, Regex.Count(page, "aspect-ratio: 800 / 600;"));
        Assert.Equal(layout is CardGridLayout.Masonry, page.Contains("<tile-masonry"));
    }

    [Fact]
    public async Task ALayoutLeftOutIsRefused()
    {
        var site = await app.MakeSiteAsync();
        var gridPage = await AddGridPageAsync(site, CardGridSettings.AllCollections);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            gridPage.SettingsUrl,
            "custom-component",
            new()
            {
                ["Input.Name"] = "Grid 0",
                ["Input.Source"] = nameof(CardGridSourceType.AllCollections),
                ["Input.HeadingType"] = nameof(CardGridHeadingType.None),
                ["Input.CardsPerPage"] = "24",
            }
        );

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var saved = Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(app.DatabaseOf(site)).GetAsync(gridPage.Grids[0]));
        Assert.Equal(CardGridSettings.AllCollections, saved.Component.Settings);
    }

    private static readonly CardGridSettings CustomList = CardGridSettings.AllCollections with
    {
        Source = new CardGridSource.ChosenCards(),
        Heading = new CardGridHeading.None(),
    };

    [Fact]
    public async Task ACustomListShowsItsVisibleCardsInTheArtistsOrder()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var collections = new CollectionRepository(app.DatabaseOf(site));
        var shown = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(shown, [CatalogTestData.CreateTestImage()]);
        var empty = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(empty, []);
        var withImage = await catalog.AddPaintingAsync("With image", [], [], [CatalogTestData.CreateTestImage()]);
        var withoutImage = await catalog.AddPaintingAsync("Without image", [], [], []);
        var gridPage = await AddGridPageAsync(site, CustomList);
        var chosen = new CardGridChosenCardRepository(app.DatabaseOf(site));
        await chosen.AddWorksAsync(gridPage.Grids[0], [withImage.Id, withoutImage.Id]);
        await chosen.AddCollectionsAsync(gridPage.Grids[0], [shown, empty]);
        var cards = await chosen.GetAsync(gridPage.Grids[0]);
        await chosen.ReorderAsync(gridPage.Grids[0], [cards[2].Id, cards[3].Id, cards[1].Id, cards[0].Id]);

        var page = await app.ClientFor(site.Host).GetStringAsync("/grid", TestContext.Current.CancellationToken);

        Assert.Equal([true, false, true, false], cards.Select(card => card.IsVisible));
        Assert.Equal([PageUrls.Collection(await SlugOfAsync(collections, shown)), $"/works/{withImage.Slug.Value}"], CardLinks(page));
    }

    [Fact]
    public async Task TheSettingsPageSaysWhyACardIsNotShown()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var empty = await catalog.AddCollectionAsync();
        var withoutImage = await catalog.AddPaintingAsync("Without image", [], [], []);
        var gridPage = await AddGridPageAsync(site, CustomList);
        var chosen = new CardGridChosenCardRepository(app.DatabaseOf(site));
        await chosen.AddCollectionsAsync(gridPage.Grids[0], [empty]);
        await chosen.AddWorksAsync(gridPage.Grids[0], [withoutImage.Id]);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var page = await client.GetStringAsync(gridPage.SettingsUrl, TestContext.Current.CancellationToken);

        Assert.Contains("2 cards", page);
        Assert.Contains($"Not shown: none of its {SiteWording.Default.Work.PluralInSentence} has an image.", page);
        Assert.Contains("Not shown: no image.", page);
        // the pages adding to the list come back to this one as it was opened
        Assert.Contains(PageUrls.AddCardGridWorks(gridPage.Grids[0], new CustomComponentOrigin(gridPage.PageId, gridPage.RowId), [])
            .Replace("&", "&amp;"), page);
    }

    [Fact]
    public async Task SwitchingToACustomListMakesAnAutomaticHeadingNone()
    {
        var site = await app.MakeSiteAsync();
        var gridPage = await AddGridPageAsync(site, CardGridSettings.AllCollections);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            gridPage.SettingsUrl,
            "custom-component",
            new()
            {
                ["Input.Name"] = "Grid 0",
                ["Input.Source"] = nameof(CardGridSourceType.ChosenCards),
                ["Input.HeadingType"] = nameof(CardGridHeadingType.Automatic),
                ["Input.CardsPerPage"] = "24",
                ["Input.HasPageLinks"] = "true",
                ["Input.Layout"] = nameof(CardGridLayout.EvenGrid),
            }
        );

        var saved = Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(app.DatabaseOf(site)).GetAsync(gridPage.Grids[0]));
        Assert.Equal(CustomList, saved.Component.Settings);
    }

    [Fact]
    public async Task AddingWorksLeavesOutTheOnesOnTheListAndReturnsToTheSettingsPage()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var onList = await catalog.AddPaintingAsync("On the list", [], [], []);
        var first = await catalog.AddPaintingAsync("First", [], [], []);
        var second = await catalog.AddPaintingAsync("Second", [], [], []);
        var gridPage = await AddGridPageAsync(site, CustomList);
        var origin = new CustomComponentOrigin(gridPage.PageId, gridPage.RowId);
        var chosen = new CardGridChosenCardRepository(app.DatabaseOf(site));
        await chosen.AddWorksAsync(gridPage.Grids[0], [onList.Id]);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var picker = await client.GetStringAsync(PageUrls.AddCardGridWorks(gridPage.Grids[0], origin, [second.Id]),
            TestContext.Current.CancellationToken);
        var response = await TestApp.PostFormAsync(client, PageUrls.AddCardGridWorks(gridPage.Grids[0], origin, [second.Id, first.Id]),
            "add-card-grid-works", []);

        Assert.Contains($"name=\"add\" value=\"{second.Id.Value}\" checked", picker);
        Assert.Contains($"name=\"add\" value=\"{first.Id.Value}\"", picker);
        Assert.DoesNotContain($"name=\"add\" value=\"{onList.Id.Value}\"", picker);
        // the filter form keeps where the settings page was opened from
        Assert.Contains($"<input type=\"hidden\" name=\"{PageUrls.ComponentRowKey}\" value=\"{gridPage.RowId.Value}\"", picker);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(PageUrls.EditCustomComponent(gridPage.Grids[0], origin), response.Headers.Location?.PathAndQuery);
        Assert.Equal(
            [onList.Id, second.Id, first.Id],
            (await chosen.GetAsync(gridPage.Grids[0])).Select(card => Assert.IsType<GridCard.OfWork>(card.Card).Id)
        );
    }

    [Fact]
    public async Task AddingCollectionsLeavesOutTheOnesOnTheList()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var onList = await catalog.AddCollectionAsync();
        var other = await catalog.AddCollectionAsync();
        var gridPage = await AddGridPageAsync(site, CustomList);
        var chosen = new CardGridChosenCardRepository(app.DatabaseOf(site));
        await chosen.AddCollectionsAsync(gridPage.Grids[0], [onList]);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var picker = await client.GetStringAsync(PageUrls.AddCardGridCollections(gridPage.Grids[0], origin: null, []),
            TestContext.Current.CancellationToken);
        var response = await TestApp.PostFormAsync(client, PageUrls.AddCardGridCollections(gridPage.Grids[0], origin: null, [other]),
            "add-card-grid-collections", []);

        Assert.Contains($"name=\"add\" value=\"{other.Value}\"", picker);
        Assert.DoesNotContain($"name=\"add\" value=\"{onList.Value}\"", picker);
        Assert.Contains("Add selected (0)", picker);
        Assert.Equal(PageUrls.EditCustomComponent(gridPage.Grids[0], origin: null), response.Headers.Location?.PathAndQuery);
        Assert.Equal(
            [onList, other],
            (await chosen.GetAsync(gridPage.Grids[0])).Select(card => Assert.IsType<GridCard.OfCollection>(card.Card).Collection.Id)
        );
    }

    // the list is kept while the grid shows another source, but only names the page while it shows the list
    [Fact]
    public async Task AWorkOnTheCustomListMentionsThePageOnlyWhileTheGridShowsIt()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var withImage = await catalog.AddPaintingAsync("With image", [], [], [CatalogTestData.CreateTestImage()]);
        var withoutImage = await catalog.AddPaintingAsync("Without image", [], [], []);
        var gridPage = await AddGridPageAsync(site, CustomList);
        var chosen = new CardGridChosenCardRepository(app.DatabaseOf(site));
        await chosen.AddWorksAsync(gridPage.Grids[0], [withImage.Id, withoutImage.Id]);
        var pages = new CustomPageRepository(app.DatabaseOf(site));
        var components = new CustomComponentRepository(app.DatabaseOf(site));

        var whileShown = await pages.GetMentioningWorkAsync(withImage.Id);
        var notVisible = await pages.GetMentioningWorkAsync(withoutImage.Id);
        await components.UpdateAsync(gridPage.Grids[0], "Grid 0", CardGridSettings.AllCollections);
        var afterSwitching = await pages.GetMentioningWorkAsync(withImage.Id);

        Assert.Equal(gridPage.PageId, Assert.Single(whileShown).Id);
        Assert.Empty(notVisible);
        Assert.Empty(afterSwitching);
        Assert.Equal(2, (await chosen.GetAsync(gridPage.Grids[0])).Count);
    }

    [Fact]
    public async Task ReorderingAListChangedElsewhereIsRefused()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var first = await catalog.AddPaintingAsync("First", [], [], []);
        var second = await catalog.AddPaintingAsync("Second", [], [], []);
        var gridPage = await AddGridPageAsync(site, CustomList);
        var chosen = new CardGridChosenCardRepository(app.DatabaseOf(site));
        await chosen.AddWorksAsync(gridPage.Grids[0], [first.Id]);
        var seen = await chosen.GetAsync(gridPage.Grids[0]);
        await chosen.AddWorksAsync(gridPage.Grids[0], [second.Id]);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            chosen.ReorderAsync(gridPage.Grids[0], [.. seen.Select(card => card.Id)]));
    }

    [Fact]
    public async Task DeletingAWorkTakesItOffTheList()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var work = await catalog.AddPaintingAsync("Work", [], [], []);
        var gridPage = await AddGridPageAsync(site, CustomList);
        var chosen = new CardGridChosenCardRepository(app.DatabaseOf(site));
        await chosen.AddWorksAsync(gridPage.Grids[0], [work.Id]);

        await new WorkRepository(app.DatabaseOf(site)).DeleteAsync(work.Id);

        Assert.Empty(await chosen.GetAsync(gridPage.Grids[0]));
    }

    // the page turns over the visible cards only, so a hidden one takes no place on any page
    [Fact]
    public async Task ACustomListPagesOverItsVisibleCards()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var first = await catalog.AddPaintingAsync("First", [], [], [CatalogTestData.CreateTestImage()]);
        var hidden = await catalog.AddPaintingAsync("Hidden", [], [], []);
        var second = await catalog.AddPaintingAsync("Second", [], [], [CatalogTestData.CreateTestImage()]);
        var gridPage = await AddGridPageAsync(site, CustomList with { CardsPerPage = 1 });
        var chosen = new CardGridChosenCardRepository(app.DatabaseOf(site));
        await chosen.AddWorksAsync(gridPage.Grids[0], [first.Id, hidden.Id, second.Id]);
        var client = app.ClientFor(site.Host);

        var secondPage = await client.GetStringAsync($"/grid?page-{gridPage.Grids[0].Value}=2", TestContext.Current.CancellationToken);
        var pastTheEnd = await client.GetAsync($"/grid?page-{gridPage.Grids[0].Value}=3", TestContext.Current.CancellationToken);

        Assert.Equal([$"/works/{second.Slug.Value}"], CardLinks(secondPage));
        Assert.Matches(CurrentPage(2), secondPage);
        Assert.Equal([1], LinkedPageNumbers(secondPage, $"page-{gridPage.Grids[0].Value}"));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, pastTheEnd.StatusCode);
    }

    // "Add selected" clicked on a picker opened before the grid was deleted
    [Theory]
    [InlineData("add-card-grid-works")]
    [InlineData("add-card-grid-collections")]
    public async Task AddingToADeletedGridGoesToItsSettingsPage(string formName)
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var work = await catalog.AddPaintingAsync("Work", [], [], []);
        var collection = await catalog.AddCollectionAsync();
        var gridPage = await AddGridPageAsync(site, CustomList);
        var origin = new CustomComponentOrigin(gridPage.PageId, gridPage.RowId);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        var url = formName == "add-card-grid-works"
            ? PageUrls.AddCardGridWorks(gridPage.Grids[0], origin, [work.Id])
            : PageUrls.AddCardGridCollections(gridPage.Grids[0], origin, [collection]);

        await new CustomComponentRepository(app.DatabaseOf(site)).DeleteAsync(gridPage.Grids[0]);
        var response = await TestApp.PostFormAsync(client, url, formName, []);
        var settingsPage = await client.GetStringAsync(gridPage.SettingsUrl, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(gridPage.SettingsUrl, response.Headers.Location?.PathAndQuery);
        Assert.Contains("This component doesn't exist.", settingsPage);
    }

    // an address edited by hand to name a component that isn't a card grid
    [Fact]
    public async Task NothingIsAddedToAComponentThatIsNotACardGrid()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
        var work = await catalog.AddPaintingAsync("Work", [], [], []);
        var gridPage = await AddGridPageAsync(site, CustomList);
        var slot = Assert.Single(Assert.Single((await new CustomPageRepository(app.DatabaseOf(site)).GetLayoutAsync(gridPage.PageId)).Rows).Slots);
        var postList = await new CustomComponentRepository(app.DatabaseOf(site)).AddToSlotAsync(slot.Id, "Posts", new PostListSettings());
        var chosen = new CardGridChosenCardRepository(app.DatabaseOf(site));

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => chosen.AddWorksAsync(postList, [work.Id]));
        Assert.Empty(await chosen.GetAsync(postList));
    }

    [Fact]
    public async Task AddingCollectionsOnASiteWithNoneSaysSo()
    {
        var site = await app.MakeSiteAsync();
        var gridPage = await AddGridPageAsync(site, CustomList);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var picker = await client.GetStringAsync(PageUrls.AddCardGridCollections(gridPage.Grids[0], origin: null, []),
            TestContext.Current.CancellationToken);

        Assert.Contains($"There are no {SiteWording.Default.Collection.PluralInSentence} yet.", picker);
    }

    [GeneratedRegex("<div class=\"invisible\" aria-hidden=\"true\">")]
    private static partial Regex Placeholder();

    private static string CurrentPage(int number) => $"data-test=\"current-page\"[^>]*>{number}<";

    [GeneratedRegex("<nav[^>]* data-test=\"page-links\".*?</nav>", RegexOptions.Singleline)]
    private static partial Regex PageLinksNav();

    [GeneratedRegex("""<a href="(?<href>/(?:collections|works)/[^"]*)" class="block">""")]
    private static partial Regex CardLink();
}
