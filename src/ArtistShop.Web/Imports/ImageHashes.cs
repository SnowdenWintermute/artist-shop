using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Imports;

public static class ImageHashes
{
    // The SHA-256 of each original still stored, by storage key: the one stored with the image, or
    // else read from the file, for an image saved without one
    public static async Task<Dictionary<string, string>> LoadAsync(
        IEnumerable<string> storageKeys,
        WorkImageRepository workImageRepository,
        ImageStorage imageStorage
    )
    {
        var onDisk = storageKeys.Distinct().Where(imageStorage.OriginalExists).ToList();
        var stored = await workImageRepository.GetSha256ByStorageKeyAsync(onDisk);
        var sha256ByStorageKey = new Dictionary<string, string>();

        foreach (var storageKey in onDisk)
        {
            sha256ByStorageKey[storageKey] = stored.TryGetValue(storageKey, out var sha256)
                ? sha256
                : await imageStorage.OriginalSha256Async(storageKey, CancellationToken.None);
        }

        return sha256ByStorageKey;
    }
}
