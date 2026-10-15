using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Images;
using ArtistShop.Web.Tests.Database;
using Microsoft.Extensions.Logging.Abstractions;
using NetVips;

namespace ArtistShop.Web.Tests.Images;

[Collection(DatabaseCollection.Name)]
public sealed class BrandImageStoreTests : IDisposable
{
    private readonly DirectoryInfo _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
    private readonly ImageStorage _imageStorage;
    private readonly BrandImageRepository _repository;
    private readonly BrandImageStore _store;

    public BrandImageStoreTests(TestDatabaseFixture database)
    {
        _imageStorage = new ImageStorage(_storageRoot.FullName);
        _imageStorage.Create();
        _repository = new BrandImageRepository(database.Site);
        _store = new BrandImageStore(
            _repository,
            _imageStorage,
            new BrandImageProcessor(TestImageProcessing.CreateAmpleLimiter(), TestImageProcessing.Settings),
            NullLogger<BrandImageStore>.Instance
        );
    }

    public void Dispose() => _storageRoot.Delete(recursive: true);

    private Task ReplaceAsync(BrandImageType type, int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3);
        return _store.ReplaceAsync(
            type,
            new MemoryStream(image.WriteToBuffer(".png")),
            $"{width}x{height}.png",
            TestContext.Current.CancellationToken
        );
    }

    private List<string> StoredOriginals() => [.. _imageStorage.ListOriginals().Select(original => original.StorageKey)];

    [Fact]
    public async Task AReplacedImagesFilesAreServedAndItsOriginalKept()
    {
        await ReplaceAsync(BrandImageType.Favicon, 64, 64);

        var favicon = (await _store.GetAllAsync())[BrandImageType.Favicon];
        Assert.Equal("64x64.png", favicon.OriginalFileName);
        var icon = favicon.File(BrandImageFileType.Raster);
        Assert.NotNull(icon);
        var served = await _store.GetFileAsync(icon.Sha256);
        Assert.Equal(BrandImageFormat.Png, served?.Format);
        Assert.Contains(Assert.Single(StoredOriginals()), await _repository.GetAllStorageKeysAsync());
    }

    [Fact]
    public async Task ReplacingDeletesTheOriginalItReplaced()
    {
        await ReplaceAsync(BrandImageType.Logo, 200, 100);
        var first = Assert.Single(StoredOriginals());

        await ReplaceAsync(BrandImageType.Logo, 300, 100);

        Assert.DoesNotContain(first, StoredOriginals());
        Assert.Single(StoredOriginals());
        Assert.Equal("300x100.png", (await _store.GetAllAsync())[BrandImageType.Logo].OriginalFileName);
    }

    [Fact]
    public async Task ARefusedImageLeavesTheCurrentOneInPlace()
    {
        await ReplaceAsync(BrandImageType.LogoOnDark, 200, 100);
        var kept = Assert.Single(StoredOriginals());

        await Assert.ThrowsAsync<UnusableImageException>(() => ReplaceAsync(BrandImageType.LogoOnDark, 200, 10));

        Assert.Equal([kept], StoredOriginals());
        Assert.Equal("200x100.png", (await _store.GetAllAsync())[BrandImageType.LogoOnDark].OriginalFileName);
    }

    [Fact]
    public async Task RemovingDeletesTheRowsAndTheOriginal()
    {
        // a size no other test uploads, so no other image has the same files
        await ReplaceAsync(BrandImageType.LogoOnDark, 210, 100);
        var sha256 = (await _store.GetAllAsync())[BrandImageType.LogoOnDark].Shown.Sha256;

        await _store.RemoveAsync(BrandImageType.LogoOnDark);

        Assert.False((await _store.GetAllAsync()).ContainsKey(BrandImageType.LogoOnDark));
        Assert.Null(await _store.GetFileAsync(sha256));
        Assert.Empty(StoredOriginals());
    }
}
