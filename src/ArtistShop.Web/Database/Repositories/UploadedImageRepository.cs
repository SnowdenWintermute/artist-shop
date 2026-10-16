namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Publishing;
using Dapper;

public class UploadedImageRepository(SiteDatabase database)
{
    public async Task<int> AddAsync(UploadedImageAddition image)
    {
        await using var connection = await database.OpenConnectionAsync();

        return await connection.QuerySingleAsync<int>(
            "SELECT add_uploaded_image(@StorageKey, @OriginalFileName, @Sha256, @Width, @Height, @BlurDataUri, @AltText)",
            new
            {
                image.StorageKey,
                image.OriginalFileName,
                image.Sha256,
                image.Width,
                image.Height,
                image.BlurDataUri,
                AltText = image.AltText.Value,
            }
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

    // newest first. A search of null lists every image
    public async Task<UploadedImageListPage> GetPageAsync(string? search, int pageNumber)
    {
        await using var connection = await database.OpenConnectionAsync();

        var pageSize = ArtistShopLimits.ImageListPageSize;

        var rows = (
            await connection.QueryAsync<ListRow>(
                "SELECT * FROM get_uploaded_image_page(@Search, @Offset, @PageSize)",
                new { Search = search, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize }
            )
        ).ToList();

        var totalCount = rows.Count is 0 ? 0 : rows[0].TotalCount;

        return new UploadedImageListPage(
            [.. rows.Select(row => new UploadedImageListItem(row.ToUploadedImage(), row.UsedInCount))],
            totalCount,
            pageNumber,
            pageSize
        );
    }

    // the posts and text components showing any of them, each once
    public async Task<List<UploadedImageUse>> GetUsesAsync(IReadOnlyCollection<string> storageKeys)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<UseRow>(
            "SELECT * FROM get_uploaded_image_uses(@StorageKeys)",
            new { StorageKeys = storageKeys.ToArray() }
        );

        return [.. rows.Select(row => new UploadedImageUse(row.Type, row.Name, row.PageNames))];
    }

    // their embeds go from every post and text component too
    public async Task DeleteAsync(IReadOnlyCollection<string> storageKeys)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync("SELECT delete_uploaded_images(@StorageKeys)", new { StorageKeys = storageKeys.ToArray() });
    }

    // false when no image has this key
    public async Task<bool> SetAltTextAsync(string storageKey, AltText altText)
    {
        await using var connection = await database.OpenConnectionAsync();

        return await connection.QuerySingleAsync<bool>(
            "SELECT set_uploaded_image_alt_text(@StorageKey, @AltText)",
            new { StorageKey = storageKey, AltText = altText.Value }
        );
    }

    public async Task<HashSet<string>> GetAllStorageKeysAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        return [.. await connection.QueryAsync<string>("SELECT * FROM get_all_uploaded_image_storage_keys()")];
    }

    private class ImageRow
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

    private sealed class UseRow
    {
        public required UploadedImageUseType Type { get; init; }
        public required string Name { get; init; }
        public required string[] PageNames { get; init; }
    }

    private sealed class ListRow : ImageRow
    {
        public required int UsedInCount { get; init; }
        public required int TotalCount { get; init; }
    }
}
