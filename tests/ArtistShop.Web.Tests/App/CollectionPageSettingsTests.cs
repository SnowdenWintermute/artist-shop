using System.Net;
using System.Text.RegularExpressions;
using ArtistShop.Web.Components;
using ArtistShop.Web.Components.CustomPages;
using ArtistShop.Web.Components.Forms;
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
    [Fact]
    public async Task ANewSiteStartsWithAnEvenGridOf24()
    {
        var site = await app.MakeSiteAsync();

        var settings = await new CollectionPageRepository(app.DatabaseOf(site)).GetAsync();

        Assert.Equal(new CollectionPageSettings(CardGridLayout.EvenGrid, CardGridSettings.DefaultCardsPerPage), settings);
    }

    [Fact]
    public async Task TheCollectionPageDrawsItsWorksWithTheSavedSettings()
    {
        var site = await app.MakeSiteAsync();
        var database = app.DatabaseOf(site);
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
            await new CollectionPageRepository(app.DatabaseOf(site)).GetAsync()
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
            await new CollectionPageRepository(app.DatabaseOf(site)).GetAsync()
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
        var database = app.DatabaseOf(site);
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

    // the Desktop and Phone buttons work in the browser, so the preview's reads happen once, with no circuit
    [Fact]
    public async Task ThePreviewIsDrawnWithoutAnIsland()
    {
        var site = await app.MakeSiteAsync();
        await new CatalogTestData(app.DatabaseOf(site)).AddCollectionAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var page = await client.GetStringAsync(PageUrls.CollectionPageSettings, TestContext.Current.CancellationToken);

        Assert.Contains($"data-preview-width=\"{CustomPageWidths.PhonePixels}\"", page);
        Assert.DoesNotContain("<!--Blazor:", page);
    }

    // so changing the layout doesn't jump the preview back to another collection
    [Fact]
    public async Task ASaveKeepsTheCollectionBeingPreviewed()
    {
        var site = await app.MakeSiteAsync();
        var catalog = new CatalogTestData(app.DatabaseOf(site));
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

    // the time travels in the address, so a reload still says when the save was
    [Fact]
    public async Task ASaveLeadsToANoticeOfItsTime()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.CollectionPageSettings,
            "collection-page",
            new()
            {
                ["Input.CardsPerPage"] = "12",
                ["Input.Layout"] = nameof(CardGridLayout.Rows),
            }
        );
        var location = Assert.IsType<Uri>(response.Headers.Location);
        var page = await client.GetStringAsync(location, TestContext.Current.CancellationToken);

        Assert.Matches($"[?&]{SavedAtQuery.Key}=\\d+", location.OriginalString);
        Assert.Matches("Saved at <local-time><time datetime=\"[^\"]+Z\">\\d{2}:\\d{2}</time></local-time>", page);
    }

    [Fact]
    public async Task ASavedValueThatIsntATimeShowsNoNotice()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var page = await client.GetStringAsync(
            $"{PageUrls.CollectionPageSettings}?{SavedAtQuery.Key}=true",
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain("Saved at", page);
    }

    // its page is still there for a visitor, and its Edit page link opens this
    [Fact]
    public async Task AnEmptyCollectionIsPreviewedAndMarkedEmpty()
    {
        var site = await app.MakeSiteAsync();
        var database = app.DatabaseOf(site);
        var catalog = new CatalogTestData(database);
        await catalog.AddPaintingInCollectionAsync(await catalog.AddCollectionAsync(), [CatalogTestData.CreateTestImage()]);
        var empty = await catalog.AddCollectionAsync();
        var emptyName = Assert.IsType<Collection>(await new CollectionRepository(database).GetByIdAsync(empty)).Name.Value;
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var page = await client.GetStringAsync(PageUrls.CollectionPageSettingsFor(empty), TestContext.Current.CancellationToken);

        Assert.Contains($"<h1 class=\"mb-4\">{emptyName}</h1>", page);
        Assert.Contains($"{emptyName} (no {SiteWording.Default.Work.PluralInSentence} with images yet)", page);
    }

    [Fact]
    public async Task WithNoCollectionsThePreviewSaysSo()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var page = await client.GetStringAsync(PageUrls.CollectionPageSettings, TestContext.Current.CancellationToken);

        Assert.Contains("Nothing to preview", page);
    }

    // an edited link, or one to a collection deleted since
    [Theory]
    [InlineData("999999")]
    [InlineData("abc")]
    public async Task AnUnknownCollectionToPreviewIsNotFound(string collection)
    {
        var site = await app.MakeSiteAsync();
        await new CatalogTestData(app.DatabaseOf(site)).AddCollectionAsync();
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await client.GetAsync(
            $"{PageUrls.CollectionPageSettings}?{PageUrls.PreviewCollectionKey}={collection}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [GeneratedRegex("""<a href="/works/[^"]*" class="block">""")]
    private static partial Regex WorkCardLink();
}
