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

// the settings of the fixed page each collection has, and that page following them
public sealed partial class CollectionPageSettingsTests(TestApp app)
{
    private SiteDatabase DatabaseOf(TestSite site) => app.Services.GetRequiredService<SiteDatabases>().For(site.Id);

    [Fact]
    public async Task ANewSiteStartsWithAnEvenGridOf24()
    {
        var site = await app.MakeSiteAsync();

        var settings = await new CollectionPageRepository(DatabaseOf(site)).GetAsync();

        Assert.Equal(new CollectionPageSettings(CardGridLayout.EvenGrid, CardGridSettings.DefaultCardsPerPage), settings);
    }

    [Fact]
    public async Task TheCollectionPageDrawsItsWorksWithTheSavedSettings()
    {
        var site = await app.MakeSiteAsync();
        var database = DatabaseOf(site);
        var catalog = new CatalogTestData(database);
        var collectionId = await catalog.AddCollectionAsync();

        for (var index = 0; index < 3; index++)
        {
            await catalog.AddPaintingInCollectionAsync(collectionId, [CatalogTestData.CreateTestImage()]);
        }

        var collection = Assert.IsType<Collection>(await new CollectionRepository(database).GetByIdAsync(collectionId));
        await new CollectionPageRepository(database).UpdateAsync(new CollectionPageSettings(CardGridLayout.Masonry, 2));

        var page = await app.ClientFor(site.Host).GetStringAsync(PageUrls.Collection(collection.Slug),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, WorkCardLink().Count(page));
        Assert.Contains("<tile-masonry", page);
        Assert.Contains("?page=2", page);
    }

    [Fact]
    public async Task TheSettingsPageSavesTheLayoutAndCardsPerPage()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        await TestApp.PostFormAsync(
            client,
            PageUrls.CollectionPageSettings,
            "collection-page",
            new()
            {
                ["Input.CardsPerPage"] = "12",
                ["Input.Layout"] = nameof(CardGridLayout.Rows),
            }
        );

        Assert.Equal(
            new CollectionPageSettings(CardGridLayout.Rows, 12),
            await new CollectionPageRepository(DatabaseOf(site)).GetAsync()
        );
    }

    [Theory]
    [InlineData(null, "12")]
    [InlineData(nameof(CardGridLayout.Rows), null)]
    [InlineData(nameof(CardGridLayout.Rows), "0")]
    public async Task ALeftOutOrOutOfRangeChoiceIsRefused(string? layout, string? cardsPerPage)
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        Dictionary<string, string> fields = [];

        if (layout is not null)
        {
            fields["Input.Layout"] = layout;
        }

        if (cardsPerPage is not null)
        {
            fields["Input.CardsPerPage"] = cardsPerPage;
        }

        var response = await TestApp.PostFormAsync(client, PageUrls.CollectionPageSettings, "collection-page", fields);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new CollectionPageSettings(CardGridLayout.EvenGrid, CardGridSettings.DefaultCardsPerPage),
            await new CollectionPageRepository(DatabaseOf(site)).GetAsync()
        );
    }

    [Fact]
    public async Task ThePagesListLinksToTheSettingsInTheSitesWording()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var page = await client.GetStringAsync(PageUrls.CustomPageList, TestContext.Current.CancellationToken);

        Assert.Contains($"href=\"{PageUrls.CollectionPageSettings}\"", page);
        Assert.Contains($"{SiteWording.Default.Collection.SingularHeading} page", page);
    }

    [Fact]
    public async Task ThePreviewShowsTheCollectionItWasOpenedFromWithTheSavedSettings()
    {
        var site = await app.MakeSiteAsync();
        var database = DatabaseOf(site);
        var catalog = new CatalogTestData(database);
        var first = await catalog.AddCollectionAsync();
        var second = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(first, [CatalogTestData.CreateTestImage()]);
        await catalog.AddPaintingInCollectionAsync(second, [CatalogTestData.CreateTestImage()]);
        var collections = new CollectionRepository(database);
        var firstName = Assert.IsType<Collection>(await collections.GetByIdAsync(first)).Name.Value;
        var secondName = Assert.IsType<Collection>(await collections.GetByIdAsync(second)).Name.Value;
        await new CollectionPageRepository(database).UpdateAsync(new CollectionPageSettings(CardGridLayout.Masonry, 2));
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var opened = await client.GetStringAsync(PageUrls.CollectionPageSettingsFor(second), TestContext.Current.CancellationToken);
        var plain = await client.GetStringAsync(PageUrls.CollectionPageSettings, TestContext.Current.CancellationToken);

        Assert.Contains($"<h1 class=\"mb-4\">{secondName}</h1>", opened);
        Assert.Contains("<tile-masonry", opened);
        Assert.Contains($"<h1 class=\"mb-4\">{firstName}</h1>", plain);
    }

    // so changing the layout doesn't jump the preview back to another collection
    [Fact]
    public async Task ASaveKeepsTheCollectionBeingPreviewed()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(DatabaseOf(site));
        await catalog.AddPaintingInCollectionAsync(await catalog.AddCollectionAsync(), [CatalogTestData.CreateTestImage()]);
        var second = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(second, [CatalogTestData.CreateTestImage()]);
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.CollectionPageSettingsFor(second),
            "collection-page",
            new()
            {
                ["Input.CardsPerPage"] = "12",
                ["Input.Layout"] = nameof(CardGridLayout.Rows),
            }
        );

        Assert.Contains($"{PageUrls.PreviewCollectionKey}={second.Value}", response.Headers.Location?.Query);
    }

    [Fact]
    public async Task WithNothingToShowThePreviewSaysSo()
    {
        var site = await app.MakeSiteAsync();
        await new CatalogTestData(DatabaseOf(site)).AddCollectionAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var page = await client.GetStringAsync(PageUrls.CollectionPageSettings, TestContext.Current.CancellationToken);

        Assert.Contains("Nothing to preview", page);
    }

    [GeneratedRegex("""<a href="/works/[^"]*" class="block">""")]
    private static partial Regex WorkCardLink();
}
