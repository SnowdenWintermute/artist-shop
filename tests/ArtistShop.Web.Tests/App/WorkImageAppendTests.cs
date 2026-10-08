using System.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;
using ArtistShop.Web.Components;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;
using NetVips;

namespace ArtistShop.Web.Tests.App;

// The whole-website import's and the bulk upload's image sent to one work, after its others
[Collection(TestAppCollection.Name)]
public sealed class WorkImageAppendTests(TestApp app)
{
    private static readonly byte[] Jpeg = CreateJpeg(ImageVariants.MinimumSourceWidth);

    private static byte[] CreateJpeg(int width)
    {
        using var image = Image.Black(width, 300, bands: 3);
        return image.WriteToBuffer(".jpg");
    }

    private async Task<(TestSite Site, CatalogTestData Catalog, WorkRepository Works, HttpClient Client)> SiteWithAdminAsync()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        return (site, new CatalogTestData(database), new WorkRepository(database), client);
    }

    private static Task<HttpResponseMessage> AppendAsync(HttpClient client, WorkId workId, byte[] bytes) =>
        TestApp.PostFileAsync(
            client,
            PageUrls.WebsiteImport,
            ImageUploadEndpoints.AppendWorkImagePath,
            "Dawn.jpg",
            "image/jpeg",
            bytes,
            new Dictionary<string, string> { ["workId"] = $"{workId.Value}" }
        );

    private static async Task<WorkImage?> ImageInAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<WorkImage>(TestContext.Current.CancellationToken);

    private int OriginalCount(TestSite site) =>
        Directory.GetFiles(ImageStorage.ForSite(app.Services.GetRequiredService<ImageStorageSettings>(), site.Id).Originals).Length;

    // an upload that got in but whose answer was lost comes again, and it's there once
    [Fact]
    public async Task TheSameImageSentTwiceIsAddedOnce()
    {
        var (site, catalog, works, client) = await SiteWithAdminAsync();
        var dawn = await catalog.AddWorkAsync(await catalog.GetPaintingTypeIdAsync(), "Dawn");

        var first = await AppendAsync(client, dawn.Id, Jpeg);
        var again = await AppendAsync(client, dawn.Id, Jpeg);
        var other = await AppendAsync(client, dawn.Id, CreateJpeg(ImageVariants.MinimumSourceWidth + 1));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK], [first.StatusCode, again.StatusCode, other.StatusCode]);
        var images = (await works.GetByIdAsync(dawn.Id))?.Images;
        Assert.NotNull(images);
        Assert.Equal(2, images.Count);
        Assert.Equal("Dawn.jpg", images[0].OriginalFileName);
        // each answer is the work's image, for its thumbnail; the repeat gets the first one's
        Assert.Equal(images[0].StorageKey, (await ImageInAsync(first))?.StorageKey);
        Assert.Equal(images[0].StorageKey, (await ImageInAsync(again))?.StorageKey);
        Assert.Equal(images[1].StorageKey, (await ImageInAsync(other))?.StorageKey);
        // the second copy was deleted rather than left for the sweep
        Assert.Equal(2, OriginalCount(site));
    }

    [Fact]
    public async Task AWorkDeletedDuringTheImportTurnsTheImageAway()
    {
        var (site, catalog, works, client) = await SiteWithAdminAsync();
        var dawn = await catalog.AddWorkAsync(await catalog.GetPaintingTypeIdAsync(), "Dawn");
        await works.DeleteAsync(dawn.Id);

        var response = await AppendAsync(client, dawn.Id, Jpeg);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The work no longer exists.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, OriginalCount(site));
    }

    [Fact]
    public async Task SomeoneWhoIsntAnAdminCantAddAnImage()
    {
        var site = await app.MakeSiteAsync();
        var database = app.Services.GetRequiredService<SiteDatabases>().For(site.Id);
        var catalog = new CatalogTestData(database);
        var dawn = await catalog.AddWorkAsync(await catalog.GetPaintingTypeIdAsync(), "Dawn");
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAccountAsync());

        // the home page's token, so only the admin check can turn it away
        var response = await TestApp.PostFileAsync(
            client,
            "/",
            ImageUploadEndpoints.AppendWorkImagePath,
            "Dawn.jpg",
            "image/jpeg",
            Jpeg,
            new Dictionary<string, string> { ["workId"] = $"{dawn.Id.Value}" }
        );

        // an endpoint a script calls is answered with a status rather than sent to a page
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var work = await new WorkRepository(database).GetByIdAsync(dawn.Id);
        Assert.NotNull(work);
        Assert.Empty(work.Images);
    }
}
