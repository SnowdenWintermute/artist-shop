using ArtistShop.Web.Components;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// the page that edits a filtered page of artworks as a table; its rows load once the circuit connects
[Collection(TestAppCollection.Name)]
public sealed class ArtworkTablePageTests(TestApp app)
{
    private async Task<(CatalogTestData Catalog, HttpClient Client)> SiteWithAdminAsync()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        return (new CatalogTestData(database), client);
    }

    [Fact]
    public async Task ShowsTheTypeAsASelectAndLoadsTheRowsAfterThePrerender()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var typeId = await catalog.GetPaintingTypeIdAsync();
        var name = $"Tabled {Guid.NewGuid():n}";
        await catalog.AddPaintingAsync(name, termIds: [], seriesIds: [], images: []);

        var page = await client.GetStringAsync($"{PageUrls.ArtworkTable}?type={typeId.Value}", TestContext.Current.CancellationToken);

        Assert.Contains("""<select name="type" """, page);
        Assert.Contains($"""<option value="{typeId.Value}" selected""", page);
        // no type checkboxes beside it
        Assert.Single(page.Split("""name="type" """).Skip(1));
        Assert.Contains("Loading artworks", page);
        Assert.DoesNotContain(name, page);
    }

    [Fact]
    public async Task TheListLinksToTheTableWithItsFiltersFromTheFirstPage()
    {
        var (catalog, client) = await SiteWithAdminAsync();
        var seriesId = await catalog.AddSeriesAsync();

        // a second page to be on; an empty one would redirect to the first
        for (var count = 0; count <= ArtistShopLimits.ArtworkListPageSize; count++)
        {
            await catalog.AddPaintingInSeriesAsync(seriesId, []);
        }

        var page = await client.GetStringAsync(
            $"{PageUrls.ArtworkList}?series={seriesId.Value}&page=2",
            TestContext.Current.CancellationToken
        );

        Assert.Contains($"""href="{PageUrls.ArtworkTable}?series={seriesId.Value}" """, page);
    }
}
