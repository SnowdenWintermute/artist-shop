using System.Net;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// the page that adds works to a collection; CollectionRepositoryTests cover where they land
[Collection(TestAppCollection.Name)]
public sealed class AddCollectionWorksPageTests(TestApp app)
{
    private async Task<(CatalogTestData Catalog, CollectionRepository Collections, HttpClient Client)> SiteWithAdminAsync()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        return (new CatalogTestData(database), new CollectionRepository(database), client);
    }

    private static string Checkbox(WorkIdentifiers work) => $"name=\"add\" value=\"{work.Id.Value}\"";

    [Fact]
    public async Task ListsOnlyTheWorksNotInTheCollectionWithTheCheckedOnesChecked()
    {
        var (catalog, _, client) = await SiteWithAdminAsync();
        var collectionId = await catalog.AddCollectionAsync();
        var inCollection = await catalog.AddPaintingInCollectionAsync(collectionId, []);
        var otherCollectionId = await catalog.AddCollectionAsync();
        var checkedWork = await catalog.AddPaintingInCollectionAsync(otherCollectionId, []);
        var uncheckedWork = await catalog.AddPaintingInCollectionAsync(otherCollectionId, []);

        var page = await client.GetStringAsync(
            PageUrls.AddCollectionWorks(collectionId, [checkedWork.Id]),
            TestContext.Current.CancellationToken
        );

        Assert.Contains($"{Checkbox(checkedWork)} checked", page);
        Assert.Contains(Checkbox(uncheckedWork), page);
        Assert.DoesNotContain($"{Checkbox(uncheckedWork)} checked", page);
        Assert.DoesNotContain(Checkbox(inCollection), page);
        Assert.Contains("Add selected (1)", page);
        // the collection filter offers the other collections, but not this one
        Assert.Contains($"<option value=\"{otherCollectionId.Value}\"", page);
        Assert.DoesNotContain($"<option value=\"{collectionId.Value}\"", page);
    }

    // a check on a work the filters hide has no box, so the forms carry it as a hidden field
    [Fact]
    public async Task KeepsACheckTheFiltersHide()
    {
        var (catalog, _, client) = await SiteWithAdminAsync();
        var collectionId = await catalog.AddCollectionAsync();
        var shownCollectionId = await catalog.AddCollectionAsync();
        await catalog.AddPaintingInCollectionAsync(shownCollectionId, []);
        var hidden = await catalog.AddPaintingInCollectionAsync(await catalog.AddCollectionAsync(), []);

        var page = await client.GetStringAsync(
            $"{PageUrls.AddCollectionWorks(collectionId, [hidden.Id])}&collection={shownCollectionId.Value}",
            TestContext.Current.CancellationToken
        );

        // once in the filter form and once in the checkbox form, and never as a box
        Assert.Equal(2, page.Split($"""<input type="hidden" {Checkbox(hidden)} />""").Length - 1);
        Assert.Equal(2, page.Split(Checkbox(hidden)).Length - 1);
    }

    [Fact]
    public async Task AddingTheCheckedWorksReturnsToTheCollection()
    {
        var (catalog, collections, client) = await SiteWithAdminAsync();
        var collectionId = await catalog.AddCollectionAsync();
        var first = await catalog.AddPaintingInCollectionAsync(collectionId, []);
        var otherCollectionId = await catalog.AddCollectionAsync();
        var second = await catalog.AddPaintingInCollectionAsync(otherCollectionId, []);
        var third = await catalog.AddPaintingInCollectionAsync(otherCollectionId, []);

        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.AddCollectionWorks(collectionId, [third.Id, second.Id]),
            "add-collection-works",
            []
        );

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(PageUrls.EditCollection(collectionId), response.Headers.Location?.AbsolutePath);
        var saved = await collections.GetAsync(collectionId);
        Assert.NotNull(saved);
        Assert.Equal([first.Id, third.Id, second.Id], saved.Works.Select(work => work.Id));
    }
}
