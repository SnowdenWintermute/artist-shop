namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Publishing;
using Dapper;

public class UploadedImageRepository(SiteDatabase database)
{
    public async Task<int> AddAsync(UploadedImageAddition image)
    {
        await using var connection = await database.OpenConnectionAsync();

        return await connection.QuerySingleAsync<int>(
            "SELECT add_uploaded_image(@StorageKey, @OriginalFileName, @Sha256, @Width, @Height, @BlurDataUri, @AltText)",
            image
        );
    }

    // by storage key. A deleted image has no entry
    public async Task<Dictionary<string, UploadedImage>> GetByStorageKeysAsync(IReadOnlyCollection<string> storageKeys)
    {
        if (storageKeys.Count == 0)
        {
            return [];
        }

        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ImageRow>(
            "SELECT * FROM get_uploaded_images_by_storage_keys(@StorageKeys)",
            new { StorageKeys = storageKeys.ToArray() }
        );

        return rows.ToDictionary(row => row.StorageKey, row => row.ToUploadedImage());
    }

    // false when no image has this key
    public async Task<bool> SetAltTextAsync(string storageKey, string altText)
    {
        await using var connection = await database.OpenConnectionAsync();

        return await connection.QuerySingleAsync<bool>(
            "SELECT set_uploaded_image_alt_text(@StorageKey, @AltText)",
            new { StorageKey = storageKey, AltText = altText }
        );
    }

    public async Task<HashSet<string>> GetAllStorageKeysAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        return [.. await connection.QueryAsync<string>("SELECT * FROM get_all_uploaded_image_storage_keys()")];
    }

    private sealed class ImageRow
    {
        public required int Id { get; init; }
        public required string StorageKey { get; init; }
        public required string OriginalFileName { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required string BlurDataUri { get; init; }
        public required string AltText { get; init; }
        // Npgsql reads a timestamptz as a DateTime in UTC
        public required DateTime UploadedAt { get; init; }

        public UploadedImage ToUploadedImage() =>
            new(new UploadedImageId(Id), StorageKey, OriginalFileName, Width, Height, BlurDataUri, AltText, new DateTimeOffset(UploadedAt));
    }
}
