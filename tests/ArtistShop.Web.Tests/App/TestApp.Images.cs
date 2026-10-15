using System.Globalization;
using System.Net.Http.Json;
using ArtistShop.Web.Components;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Images;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.DependencyInjection;
using NetVips;

namespace ArtistShop.Web.Tests.App;

public sealed record TestWorkImage(TestSite Site, WorkSlug WorkSlug, string StorageKey);

public sealed partial class TestApp
{
    // a new site with one work, whose one image was uploaded as an admin uploads it, every size made
    public async Task<TestWorkImage> MakeSiteWithAWorkImageAsync(int imageWidth)
    {
        var site = await MakeSiteAsync();
        var catalog = new CatalogTestData(Services.GetRequiredService<SiteDatabases>().For(site.Id));
        var client = await SignedInClientAsync(site.Host, await MakeAdminAsync(site.Id));
        var work = await catalog.AddWorkAsync(await catalog.GetPaintingTypeIdAsync(), "Dawn");

        using var image = Image.Black(imageWidth, 600, bands: 3);
        var response = await PostFileAsync(
            client,
            PageUrls.WebsiteImport,
            ImageUploadEndpoints.AppendWorkImagePath,
            "Dawn.jpg",
            "image/jpeg",
            image.WriteToBuffer(".jpg"),
            new Dictionary<string, string> { ["workId"] = $"{work.Id.Value}" }
        );
        var stored = await response.Content.ReadFromJsonAsync<WorkImage>(TestContext.Current.CancellationToken);

        return new TestWorkImage(
            site,
            work.Slug,
            stored?.StorageKey ?? throw new InvalidOperationException("The upload didn't answer with the image.")
        );
    }

    public ImageStorage ImageStorageOf(TestSite site) =>
        ImageStorage.ForSite(Services.GetRequiredService<ImageStorageSettings>(), site.Id);

    // both formats of one size, with the image still marked, as a crash part way through its sizes
    // leaves it
    public void DeleteImageSize(TestSite site, string storageKey, int width)
    {
        ImageStorageOf(site).MarkSizesPending(storageKey);

        var directory = Path.Combine(
            _imageStorageRootPath,
            "sites",
            site.Id.Value.ToString(CultureInfo.InvariantCulture),
            "variants",
            storageKey
        );

        File.Delete(Path.Combine(directory, ImageVariants.FileName(width, ImageVariantFormat.Avif)));
        File.Delete(Path.Combine(directory, ImageVariants.FileName(width, ImageVariantFormat.Webp)));
    }
}
