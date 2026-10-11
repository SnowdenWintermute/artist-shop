namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Website;
using Dapper;

public record BrandImageFileContent(BrandImageFile File, byte[] Bytes);

public class BrandImageRepository(SiteDatabase database)
{
    public async Task<Dictionary<BrandImageType, BrandImage>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<FileRow>("SELECT * FROM get_all_brand_image_files()");

        return rows.GroupBy(row => row.ImageType)
            .ToDictionary(
                group => group.Key,
                group => new BrandImage(
                    group.Key,
                    group.First().OriginalFileName,
                    [.. group.Select(row => new BrandImageFile(row.Type, row.Format, row.Width, row.Height, row.Sha256))]
                )
            );
    }

    // null when no file has these bytes, as when the image was replaced after the page was sent
    public async Task<(BrandImageFormat Format, byte[] Bytes)?> GetFileAsync(string sha256)
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleOrDefaultAsync<ContentRow>(
            "SELECT * FROM get_brand_image_file(@Sha256)",
            new { Sha256 = sha256 }
        );

        return row is null ? null : (row.Format, row.Bytes);
    }

    // returns the storage key of the original replaced, for the caller to delete
    public async Task<string?> ReplaceAsync(
        BrandImageType type,
        string storageKey,
        string originalFileName,
        string sha256,
        IReadOnlyList<BrandImageFileContent> files
    )
    {
        await using var connection = await database.OpenConnectionAsync();

        return await connection.QuerySingleAsync<string?>(
            "SELECT replace_brand_image(@Type, @StorageKey, @OriginalFileName, @Sha256, @Files)",
            new
            {
                Type = type,
                StorageKey = storageKey,
                OriginalFileName = originalFileName,
                Sha256 = sha256,
                Files = files
                    .Select(content => new BrandImageFileInput(
                        (int)content.File.Type,
                        (int)content.File.Format,
                        content.File.Width,
                        content.File.Height,
                        content.File.Sha256,
                        content.Bytes
                    ))
                    .ToArray(),
            }
        );
    }

    // returns the removed original's storage key, for the caller to delete
    public async Task<string?> RemoveAsync(BrandImageType type)
    {
        await using var connection = await database.OpenConnectionAsync();

        return await connection.QuerySingleAsync<string?>("SELECT remove_brand_image(@Type)", new { Type = type });
    }

    public async Task<List<BrandImageOriginal>> GetAllOriginalsAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<OriginalRow>("SELECT * FROM get_all_brand_image_originals()");

        return [.. rows.Select(row => new BrandImageOriginal(row.Type, row.StorageKey, row.OriginalFileName, row.Sha256))];
    }

    public async Task<HashSet<string>> GetAllStorageKeysAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        return [.. await connection.QueryAsync<string>("SELECT * FROM get_all_brand_image_storage_keys()")];
    }

    private sealed class FileRow
    {
        public required BrandImageType ImageType { get; init; }
        public required string OriginalFileName { get; init; }
        public required BrandImageFileType Type { get; init; }
        public required BrandImageFormat Format { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required string Sha256 { get; init; }
    }

    private sealed class OriginalRow
    {
        public required BrandImageType Type { get; init; }
        public required string StorageKey { get; init; }
        public required string OriginalFileName { get; init; }
        public required string Sha256 { get; init; }
    }

    private sealed class ContentRow
    {
        public required BrandImageFormat Format { get; init; }
        public required byte[] Bytes { get; init; }
    }
}
