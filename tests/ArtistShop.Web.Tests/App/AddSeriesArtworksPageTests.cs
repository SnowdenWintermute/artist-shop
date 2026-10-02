using System.Net;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// the page that adds artworks to a series; SeriesRepositoryTests cover where they land
[Collection(TestAppCollection.Name)]
public sealed class AddSeriesArtworksPageTests(TestApp app)
{
    private async Task<(CatalogTestData Catalog, SeriesRepository Series, HttpClient Client)> SiteWithAdminAsync()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        return (new CatalogTestData(database), new SeriesRepository(database), client);
    }

    private static string TickBox(ArtworkIdentifiers artwork) => $"name=\"add\" value=\"{artwork.Id.Value}\"";

    [Fact]
    public async Task ListsOnlyTheArtworksNotInTheSeriesWithTheTickedOnesTicked()
    {
        var (catalog, _, client) = await SiteWithAdminAsync();
        var seriesId = await catalog.AddSeriesAsync();
        var inSeries = await catalog.AddPaintingInSeriesAsync(seriesId, []);
        var otherSeriesId = await catalog.AddSeriesAsync();
        var ticked = await catalog.AddPaintingInSeriesAsync(otherSeriesId, []);
        var unticked = await catalog.AddPaintingInSeriesAsync(otherSeriesId, []);

        var page = await client.GetStringAsync(
            PageUrls.AddSeriesArtworks(seriesId, [ticked.Id]),
            TestContext.Current.CancellationToken
        );

        Assert.Contains($"{TickBox(ticked)} checked", page);
        Assert.Contains(TickBox(unticked), page);
        Assert.DoesNotContain($"{TickBox(unticked)} checked", page);
        Assert.DoesNotContain(TickBox(inSeries), page);
        Assert.Contains("Add selected (1)", page);
        // the series filter offers the other series, but not this one
        Assert.Contains($"<option value=\"{otherSeriesId.Value}\"", page);
        Assert.DoesNotContain($"<option value=\"{seriesId.Value}\"", page);
    }

    // a tick on an artwork the filters hide has no box, so the forms carry it as a hidden field
    [Fact]
    public async Task KeepsATickTheFiltersHide()
    {
        var (catalog, _, client) = await SiteWithAdminAsync();
        var seriesId = await catalog.AddSeriesAsync();
        var shownSeriesId = await catalog.AddSeriesAsync();
        await catalog.AddPaintingInSeriesAsync(shownSeriesId, []);
        var hidden = await catalog.AddPaintingInSeriesAsync(await catalog.AddSeriesAsync(), []);

        var page = await client.GetStringAsync(
            $"{PageUrls.AddSeriesArtworks(seriesId, [hidden.Id])}&series={shownSeriesId.Value}",
            TestContext.Current.CancellationToken
        );

        // once in the filter form and once in the tick form, and never as a box
        Assert.Equal(2, page.Split($"""<input type="hidden" {TickBox(hidden)} />""").Length - 1);
        Assert.Equal(2, page.Split(TickBox(hidden)).Length - 1);
    }

    [Fact]
    public async Task AddingTheTickedArtworksReturnsToTheSeries()
    {
        var (catalog, series, client) = await SiteWithAdminAsync();
        var seriesId = await catalog.AddSeriesAsync();
        var first = await catalog.AddPaintingInSeriesAsync(seriesId, []);
        var otherSeriesId = await catalog.AddSeriesAsync();
        var second = await catalog.AddPaintingInSeriesAsync(otherSeriesId, []);
        var third = await catalog.AddPaintingInSeriesAsync(otherSeriesId, []);

        var response = await TestApp.PostFormAsync(
            client,
            PageUrls.AddSeriesArtworks(seriesId, [third.Id, second.Id]),
            "add-series-artworks",
            []
        );

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(PageUrls.EditSeries(seriesId), response.Headers.Location?.AbsolutePath);
        var saved = await series.GetAsync(seriesId);
        Assert.NotNull(saved);
        Assert.Equal([first.Id, third.Id, second.Id], saved.Artworks.Select(artwork => artwork.Id));
    }
}
