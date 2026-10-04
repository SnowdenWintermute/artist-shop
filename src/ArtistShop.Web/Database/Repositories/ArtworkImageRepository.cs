namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class ArtworkImageRepository(SiteDatabase database)
{
    // since we're using this to determine if an image exists, we
    // pick hash set
    public async Task<HashSet<string>> GetAllStorageKeysAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        // when a result set has exactly one column,
        // QueryAsync maps each row to a string, if it
        // had more columns we would create a class to
        // represent that row's data in C#
        var storageKeys = await connection.QueryAsync<string>(
            "SELECT * FROM get_all_artwork_image_storage_keys()"
        );

        return [.. storageKeys];
    }

    public async Task<ImageAttachResult> AttachPrimaryImageToImagelessArtworkByNameAsync(
        ArtworkTypeId typeId,
        ArtworkName artworkName,
        ArtworkImage image,
        string sha256
    )
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            var row = await connection.QuerySingleAsync<AttachRow>(
                """
                SELECT * FROM attach_primary_image_to_imageless_artwork_by_name(
                    @ArtworkTypeId, @ArtworkName, @StorageKey, @OriginalFileName, @Width, @Height, @BlurDataUri, @Sha256
                )
                """,
                new
                {
                    ArtworkTypeId = typeId.Value,
                    ArtworkName = artworkName.Value,
                    image.StorageKey,
                    image.OriginalFileName,
                    image.Width,
                    image.Height,
                    image.BlurDataUri,
                    Sha256 = sha256,
                }
            );

            return new ImageAttachResult(
                row.MatchType,
                [.. row.ArtworkIds.Select(id => new ArtworkId(id))]
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworkTypeNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // After the artwork's other images, and its primary when it has none. False, adding nothing,
    // when the artwork already has an image with this hash. Throws ChangedSincePageLoadException
    // when the artwork is gone
    // the image the artwork holds afterwards: this one, or the one it already had with this hash
    public async Task<ImageAppendResult> AppendImageAsync(ArtworkId artworkId, ArtworkImage image, string sha256)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            var row = await connection.QuerySingleAsync<AppendRow>(
                "SELECT * FROM append_artwork_image(@ArtworkId, @StorageKey, @OriginalFileName, @Width, @Height, @BlurDataUri, @Sha256)",
                new
                {
                    ArtworkId = artworkId.Value,
                    image.StorageKey,
                    image.OriginalFileName,
                    image.Width,
                    image.Height,
                    image.BlurDataUri,
                    Sha256 = sha256,
                }
            );

            return new ImageAppendResult(
                new ArtworkImage(row.StorageKey, row.OriginalFileName, row.Width, row.Height, row.BlurDataUri),
                row.Appended
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.ArtworkNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // the hashes stored for these images, by storage key; an image saved without one is left out
    public async Task<Dictionary<string, string>> GetSha256ByStorageKeyAsync(IReadOnlyCollection<string> storageKeys)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<Sha256Row>(
            "SELECT * FROM get_artwork_image_sha256s(@StorageKeys)",
            new { StorageKeys = storageKeys.ToArray() }
        );

        return rows.ToDictionary(row => row.StorageKey, row => row.Sha256);
    }

    // the file names each artwork's images were uploaded under
    public async Task<ILookup<ArtworkId, string>> GetFileNamesAsync(IReadOnlyCollection<ArtworkId> artworkIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<FileNameRow>(
            "SELECT * FROM get_artwork_image_file_names(@ArtworkIds)",
            new { ArtworkIds = (int[])[.. artworkIds.Select(artworkId => artworkId.Value)] }
        );

        return rows.ToLookup(row => new ArtworkId(row.ArtworkId), row => row.OriginalFileName);
    }

    // the names must be distinct ignoring case, or one name's rows would come back under both spellings
    public async Task<Dictionary<ArtworkName, ArtworkNameMatch>> GetArtworkNameMatchesAsync(
        ArtworkTypeId typeId,
        IReadOnlyCollection<ArtworkName> artworkNames
    )
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await using var results = await connection.QueryMultipleAsync(
                """
                SELECT * FROM get_artwork_name_match_types(@ArtworkTypeId, @ArtworkNames);
                SELECT * FROM get_artwork_name_matches(@ArtworkTypeId, @ArtworkNames);
                """,
                new
                {
                    ArtworkTypeId = typeId.Value,
                    ArtworkNames = (string[])
                        [.. artworkNames.Select(artworkName => artworkName.Value)],
                }
            );

            var matchTypes = await results.ReadAsync<NameMatchTypeRow>();
            var matchedArtworks = await results.ReadAsync<NameArtworkRow>();

            // ToLookup groups rows by a key, like a dictionary whose values are lists;
            // a missing key gives an empty list rather than throwing
            var artworkIdsByName = matchedArtworks.ToLookup(
                row => row.Name,
                row => new ArtworkId(row.ArtworkId)
            );

            // the functions return each name exactly as it was sent, so plain string keys line up
            return matchTypes.ToDictionary(
                row => new ArtworkName(row.Name),
                row => new ArtworkNameMatch(row.MatchType, [.. artworkIdsByName[row.Name]])
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworkTypeNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    private sealed class Sha256Row
    {
        public required string StorageKey { get; init; }
        public required string Sha256 { get; init; }
    }

    private sealed class FileNameRow
    {
        public required int ArtworkId { get; init; }
        public required string OriginalFileName { get; init; }
    }

    private sealed class AppendRow
    {
        public required string StorageKey { get; init; }
        public string? OriginalFileName { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public string? BlurDataUri { get; init; }
        public required bool Appended { get; init; }
    }

    private sealed class AttachRow
    {
        public required ArtworkNameMatchType MatchType { get; init; }
        public required int[] ArtworkIds { get; init; }
    }

    private sealed class NameMatchTypeRow
    {
        public required string Name { get; init; }
        public required ArtworkNameMatchType MatchType { get; init; }
    }

    private sealed class NameArtworkRow
    {
        public required string Name { get; init; }
        public required int ArtworkId { get; init; }
    }
}
