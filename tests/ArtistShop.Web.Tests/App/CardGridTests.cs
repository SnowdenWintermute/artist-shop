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
    private SiteDatabase DatabaseOf(TestSite site) => app.Services.GetRequiredService<SiteDatabases>().For(site.Id);

    private sealed record GridPage(CustomPageId PageId, PageRowId RowId, IReadOnlyList<CustomComponentId> Grids)
    {
        public string SettingsUrl => PageUrls.EditCustomComponent(Grids[0], PageId, RowId);
    }

    // a page at /grid whose one row holds the grids side by side
    private async Task<GridPage> AddGridPageAsync(TestSite site, params CardGridSettings[] grids)
    {
        var database = DatabaseOf(site);
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

    [Fact]
    public async Task OneCollectionsWorksAreHeadedByItsNameAndLinkWithinIt()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(DatabaseOf(site));
        var collectionId = await catalog.AddCollectionAsync();
        var work = await catalog.AddPaintingInCollectionAsync(collectionId, [CatalogTestData.CreateTestImage()]);
        var collection = Assert.IsType<Collection>(await new CollectionRepository(DatabaseOf(site)).GetByIdAsync(collectionId));
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
        var catalog = new CatalogTestData(DatabaseOf(site));
        var collectionId = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(collectionId, [CatalogTestData.CreateTestImage()]);
        var older = await catalog.AddPaintingAsync("Older", [], [], [CatalogTestData.CreateTestImage()]);
        var newer = await catalog.AddPaintingAsync("Newer", [], [], [CatalogTestData.CreateTestImage()]);
        await catalog.AddPaintingAsync("No image", [], [], []);
        var collectionSlug = await SlugOfAsync(new CollectionRepository(DatabaseOf(site)), collectionId);
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
    public async Task TwoGridsOnOnePageTurnTheirPagesSeparately()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(DatabaseOf(site));
        var collections = new CollectionRepository(DatabaseOf(site));
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
        var catalog = new CatalogTestData(DatabaseOf(site));
        var first = await catalog.AddCollectionAsync();
        var second = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(first, [CatalogTestData.CreateTestImage()]);
        await catalog.AddPaintingInCollectionAsync(second, [CatalogTestData.CreateTestImage()]);
        var gridPage = await AddGridPageAsync(site, CardGridSettings.AllCollections with { CardsPerPage = 1, HasPageLinks = false });

        var page = await app.ClientFor(site.Host)
            .GetStringAsync($"/grid?page-{gridPage.Grids[0].Value}=2", TestContext.Current.CancellationToken);

        Assert.Equal([PageUrls.Collection(await SlugOfAsync(new CollectionRepository(DatabaseOf(site)), first))], CardLinks(page));
        Assert.DoesNotContain("Page 1 of", page);
    }

    [Fact]
    public async Task TheSettingsPageSavesASourceAndACustomHeading()
    {
        var site = await app.MakeSiteAsync();
        var collectionId = await new CatalogTestData(DatabaseOf(site)).AddCollectionAsync();
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
            }
        );

        var saved = Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(DatabaseOf(site)).GetAsync(gridPage.Grids[0]));
        Assert.Equal(
            new CardGridSettings(
                new CardGridSource.WorksInCollection(collectionId),
                new CardGridHeading.Custom("Recent work"),
                3,
                HasPageLinks: false,
                HasSameSizeCards: false
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
            }
        );

        Assert.Contains("Choose one.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var saved = Assert.IsType<CustomComponentInUse>(await new CustomComponentRepository(DatabaseOf(site)).GetAsync(gridPage.Grids[0]));
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
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task SameSizeCardsFillAShortLastPage(bool hasSameSizeCards, int placeholderCount)
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(DatabaseOf(site));

        for (var index = 0; index < 3; index++)
        {
            await catalog.AddPaintingInCollectionAsync(await catalog.AddCollectionAsync(), [CatalogTestData.CreateTestImage()]);
        }

        var gridPage = await AddGridPageAsync(site,
            CardGridSettings.AllCollections with { CardsPerPage = 2, HasSameSizeCards = hasSameSizeCards });
        var visitor = app.ClientFor(site.Host);

        var first = await visitor.GetStringAsync("/grid", TestContext.Current.CancellationToken);
        var last = await visitor.GetStringAsync($"/grid?page-{gridPage.Grids[0].Value}=2", TestContext.Current.CancellationToken);

        Assert.Equal(0, Placeholder().Count(first));
        Assert.Single(CardLinks(last));
        Assert.Equal(placeholderCount, Placeholder().Count(last));
        // an empty title line has no height, which made a placeholder shorter than a card
        Assert.DoesNotContain("title=\"\"></span>", last);
    }

    [GeneratedRegex("<div class=\"invisible\" aria-hidden=\"true\">")]
    private static partial Regex Placeholder();

    [GeneratedRegex("""<a href="(?<href>/(?:collections|works)/[^"]*)" class="block">""")]
    private static partial Regex CardLink();
}
