namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class WorkImageRepository(SiteDatabase database)
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
            "SELECT * FROM get_all_work_image_storage_keys()"
        );

        return [.. storageKeys];
    }

    public async Task<ImageAttachResult> AttachPrimaryImageToImagelessWorkByNameAsync(
        WorkTypeId typeId,
        WorkName workName,
        WorkImage image,
        string sha256
    )
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            var row = await connection.QuerySingleAsync<AttachRow>(
                """
                SELECT * FROM attach_primary_image_to_imageless_work_by_name(
                    @WorkTypeId, @WorkName, @StorageKey, @OriginalFileName, @Width, @Height, @BlurDataUri, @Sha256
                )
                """,
                new
                {
                    WorkTypeId = typeId.Value,
                    WorkName = workName.Value,
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
                [.. row.WorkIds.Select(id => new WorkId(id))]
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.WorkTypeNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // After the work's other images, and its primary when it has none. False, adding nothing,
    // when the work already has an image with this hash. Throws ChangedSincePageLoadException
    // when the work is gone
    // the image the work holds afterwards: this one, or the one it already had with this hash
    public async Task<ImageAppendResult> AppendImageAsync(WorkId workId, WorkImage image, string sha256)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            var row = await connection.QuerySingleAsync<AppendRow>(
                "SELECT * FROM append_work_image(@WorkId, @StorageKey, @OriginalFileName, @Width, @Height, @BlurDataUri, @Sha256)",
                new
                {
                    WorkId = workId.Value,
                    image.StorageKey,
                    image.OriginalFileName,
                    image.Width,
                    image.Height,
                    image.BlurDataUri,
                    Sha256 = sha256,
                }
            );

            return new ImageAppendResult(
                new WorkImage(row.StorageKey, row.OriginalFileName, row.Width, row.Height, row.BlurDataUri),
                row.Appended
            );
        }
        catch (PostgresException exception) when (SqlErrors.IsThrown(exception, SqlStates.WorkNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.MessageText, exception);
        }
    }

    // the hashes stored for these images, by storage key; an image saved without one is left out
    public async Task<Dictionary<string, string>> GetSha256ByStorageKeyAsync(IReadOnlyCollection<string> storageKeys)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<Sha256Row>(
            "SELECT * FROM get_work_image_sha256s(@StorageKeys)",
            new { StorageKeys = storageKeys.ToArray() }
        );

        return rows.ToDictionary(row => row.StorageKey, row => row.Sha256);
    }

    // the file names each work's images were uploaded under
    public async Task<ILookup<WorkId, string>> GetFileNamesAsync(IReadOnlyCollection<WorkId> workIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<FileNameRow>(
            "SELECT * FROM get_work_image_file_names(@WorkIds)",
            new { WorkIds = (int[])[.. workIds.Select(workId => workId.Value)] }
        );

        return rows.ToLookup(row => new WorkId(row.WorkId), row => row.OriginalFileName);
    }

    // the names must be distinct ignoring case, or one name's rows would come back under both spellings
    public async Task<Dictionary<WorkName, WorkNameMatch>> GetWorkNameMatchesAsync(
        WorkTypeId typeId,
        IReadOnlyCollection<WorkName> workNames
    )
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await using var results = await connection.QueryMultipleAsync(
                """
                SELECT * FROM get_work_name_match_types(@WorkTypeId, @WorkNames);
                SELECT * FROM get_work_name_matches(@WorkTypeId, @WorkNames);
                """,
                new
                {
                    WorkTypeId = typeId.Value,
                    WorkNames = (string[])
                        [.. workNames.Select(workName => workName.Value)],
                }
            );

            var matchTypes = await results.ReadAsync<NameMatchTypeRow>();
            var matchedWorks = await results.ReadAsync<NameWorkRow>();

            // ToLookup groups rows by a key, like a dictionary whose values are lists;
            // a missing key gives an empty list rather than throwing
            var workIdsByName = matchedWorks.ToLookup(
                row => row.Name,
                row => new WorkId(row.WorkId)
            );

            // the functions return each name exactly as it was sent, so plain string keys line up
            return matchTypes.ToDictionary(
                row => new WorkName(row.Name),
                row => new WorkNameMatch(row.MatchType, [.. workIdsByName[row.Name]])
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.WorkTypeNoLongerExists))
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
        public required int WorkId { get; init; }
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
        public required WorkNameMatchType MatchType { get; init; }
        public required int[] WorkIds { get; init; }
    }

    private sealed class NameMatchTypeRow
    {
        public required string Name { get; init; }
        public required WorkNameMatchType MatchType { get; init; }
    }

    private sealed class NameWorkRow
    {
        public required string Name { get; init; }
        public required int WorkId { get; init; }
    }
}
