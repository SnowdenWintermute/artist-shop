namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class ArtworkImageRepository(NpgsqlDataSource dataSource)
{
    // since we're using this to determine if an image exists, we
    // pick hash set
    public async Task<HashSet<string>> GetAllStorageKeysAsync()
    {
        await using var connection = dataSource.CreateConnection();

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
        ArtworkImage image
    )
    {
        await using var connection = dataSource.CreateConnection();

        try
        {
            var row = await connection.QuerySingleAsync<AttachRow>(
                """
                SELECT * FROM attach_primary_image_to_imageless_artwork_by_name(
                    @ArtworkTypeId, @ArtworkName, @StorageKey, @OriginalFileName, @Width, @Height, @BlurDataUri
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
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    // the names must be distinct ignoring case, or one name's rows would come back under both spellings
    public async Task<Dictionary<ArtworkName, ArtworkNameMatch>> GetArtworkNameMatchesAsync(
        ArtworkTypeId typeId,
        IReadOnlyCollection<ArtworkName> artworkNames
    )
    {
        await using var connection = dataSource.CreateConnection();

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
            throw new CatalogChangedException(exception.Message, exception);
        }
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
