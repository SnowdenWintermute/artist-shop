using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Tests.Database;

public static class UploadedImageTestData
{
    // a library image's row, with no file behind it; its storage key
    public static async Task<string> AddAsync(UploadedImageRepository library, string originalFileName, string altText)
    {
        var storageKey = ImageStorage.NewStorageKey();
        await library.AddAsync(
            new UploadedImageAddition(storageKey, originalFileName, new string('0', 64), 1200, 800, "data:image/webp;base64,AAAA", Unwrap.Value(AltText.Read(altText)))
        );
        return storageKey;
    }
}
