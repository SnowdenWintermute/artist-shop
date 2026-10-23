using System.Net;
using System.Net.Http.Json;
using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Pages.Admin.Publishing.Images;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using Microsoft.Extensions.DependencyInjection;
using NetVips;

namespace ArtistShop.Web.Tests.App;

public sealed class ImageLibraryTests(TestApp app)
{
    private const string FileName = "Dawn.jpg";

    private async Task<(TestSite Site, HttpClient Client)> SiteWithAdminAsync()
    {
        var site = await app.MakeSiteAsync();
        return (site, await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id)));
    }

    private UploadedImageRepository Library(TestSite site) =>
        new(app.DatabaseOf(site));

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client)
    {
        using var image = Image.Black(800, 600, bands: 3);

        return await TestApp.PostFileAsync(
            client,
            PageUrls.NewPost,
            ImageUploadEndpoints.LibraryImageUploadPath,
            FileName,
            "image/jpeg",
            image.WriteToBuffer(".jpg"),
            []
        );
    }

    private static async Task<HttpResponseMessage> SetAltTextAsync(HttpClient client, string storageKey, string altText) =>
        await client.PostAsync(
            UploadedImageEndpoints.AltTextPath,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    [UploadedImageEndpoints.StorageKeyField] = storageKey,
                    [UploadedImageEndpoints.AltTextField] = altText,
                    ["__RequestVerificationToken"] = await TestApp.AntiforgeryTokenAsync(client, PageUrls.NewPost),
                }
            ),
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task AnImageUploadedInTheTextEditorGoesIntoTheLibrary()
    {
        var (site, client) = await SiteWithAdminAsync();

        var response = await UploadAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var uploaded = await response.Content.ReadFromJsonAsync<LibraryImageUploadResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(uploaded);
        Assert.Equal(Path.GetFileNameWithoutExtension(FileName), uploaded.AltText);
        Assert.Contains(uploaded.StorageKey, await Library(site).GetAllStorageKeysAsync());
    }

    // the toolbar saves it to the library image, so the post shows it without being saved again
    [Fact]
    public async Task AltTextSavedFromTheToolbarShowsOnThePost()
    {
        var (site, client) = await SiteWithAdminAsync();
        var uploaded = await (await UploadAsync(client)).Content.ReadFromJsonAsync<LibraryImageUploadResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(uploaded);
        var slug = new PostSlug("pier");
        await new PostRepository(app.DatabaseOf(site)).AddAsync(
            new PostTitle("Pier"),
            slug,
            new RichText("""{"ops":[{"insert":{"artshop-image":{"storageKey":"KEY"}}},{"insert":"\n"}]}""".Replace("KEY", uploaded.StorageKey)),
            PostStatus.Published
        );
        const string altText = "The pier at dusk";

        var response = await SetAltTextAsync(client, uploaded.StorageKey, $" {altText} ");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var page = await app.ClientFor(site.Host).GetStringAsync(PageUrls.Post(slug), TestContext.Current.CancellationToken);
        Assert.Contains($"alt=\"{altText}\"", page);
    }

    [Fact]
    public async Task AltTextForAnImageNotInTheLibraryIsNotFound()
    {
        var (_, client) = await SiteWithAdminAsync();

        var response = await SetAltTextAsync(client, ImageStorage.NewStorageKey(), "Gone");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheImagesPageListsAnUploadWithItsAltText()
    {
        var (_, client) = await SiteWithAdminAsync();
        var uploaded = await (await UploadAsync(client)).Content.ReadFromJsonAsync<LibraryImageUploadResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(uploaded);

        var page = await client.GetStringAsync(PageUrls.Images, TestContext.Current.CancellationToken);

        Assert.Contains("data-test=\"alt-text\"", page);
        Assert.Contains($"value=\"{uploaded.AltText}\"", page);
        Assert.Contains(FileName, page);
        Assert.Contains($"data-hint=\"{nameof(HintType.Images)}\"", page);
    }

    // the editor's image picker lists the library in its frame, searched as the Images page is
    [Fact]
    public async Task ThePickerListsLibraryImagesMatchingItsSearch()
    {
        var (_, client) = await SiteWithAdminAsync();
        var uploaded = await (await UploadAsync(client)).Content.ReadFromJsonAsync<LibraryImageUploadResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(uploaded);

        var page = await client.GetStringAsync(PageUrls.ImagePicker, TestContext.Current.CancellationToken);
        var searched = await client.GetStringAsync(
            $"{PageUrls.ImagePicker}?{ImageSearchForm.SearchKey}=nothing-has-this",
            TestContext.Current.CancellationToken
        );

        Assert.Contains("data-test=\"library-image\"", page);
        Assert.Contains($"data-storage-key=\"{uploaded.StorageKey}\"", page);
        Assert.DoesNotContain("data-test=\"library-image\"", searched);
    }
}
