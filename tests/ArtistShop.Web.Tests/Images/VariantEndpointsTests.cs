using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Images;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ArtistShop.Web.Tests.Images;

public sealed class VariantEndpointsTests : IDisposable
{
    private const string StorageKey = "0123456789abcdef0123456789abcdef";

    private readonly DirectoryInfo _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
    private readonly ImageStorageSettings _storageSettings;
    private readonly ImageStorage _imageStorage;

    public VariantEndpointsTests()
    {
        _storageSettings = new ImageStorageSettings(_storageRoot.FullName);
        _imageStorage = ImageStorage.ForSite(_storageSettings, new SiteId(1));
        Directory.CreateDirectory(_imageStorage.VariantDirectory(StorageKey));
        File.WriteAllText(Path.Combine(_imageStorage.VariantDirectory(StorageKey), "800.avif"), "a variant");
    }

    public void Dispose() => _storageRoot.Delete(recursive: true);

    [Fact]
    public void ServesAVariantWithItsTypeAndAWeeksCaching()
    {
        var response = new DefaultHttpContext().Response;

        var result = VariantEndpoints.Serve(StorageKey, "800.avif", _imageStorage, response).Result;

        var file = Assert.IsType<PhysicalFileHttpResult>(result);
        Assert.Equal(Path.Combine(_imageStorage.VariantDirectory(StorageKey), "800.avif"), file.FileName);
        Assert.Equal("image/avif", file.ContentType);
        Assert.Equal("public, max-age=604800, immutable", response.Headers.CacheControl);
    }

    [Fact]
    public void AnotherSitesStorageHasNoSuchFile()
    {
        var otherSite = ImageStorage.ForSite(_storageSettings, new SiteId(2));

        var result = VariantEndpoints.Serve(StorageKey, "800.avif", otherSite, new DefaultHttpContext().Response).Result;

        Assert.IsType<NotFound>(result);
    }

    [Theory]
    [InlineData(StorageKey, "400.avif")]
    [InlineData(StorageKey, "800.png")]
    [InlineData(StorageKey, "800.avif\n")]
    [InlineData(StorageKey, "..")]
    [InlineData("..", "800.avif")]
    [InlineData("not-a-storage-key", "800.avif")]
    public void AnythingElseIsNotFound(string storageKey, string fileName)
    {
        var response = new DefaultHttpContext().Response;

        var result = VariantEndpoints.Serve(storageKey, fileName, _imageStorage, response).Result;

        Assert.IsType<NotFound>(result);
        Assert.Empty(response.Headers.CacheControl.ToString());
    }
}
