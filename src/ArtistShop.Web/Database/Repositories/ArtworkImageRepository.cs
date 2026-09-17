namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Microsoft.Data.SqlClient;

public class ArtworkImageRepository(SqlConnectionFactory connectionFactory)
{
    // since we're using this to determine if an image exists, we
    // pick hash set
    public async Task<HashSet<string>> GetAllStorageKeysAsync()
    {
        await using var connection = connectionFactory.Create();

        // when a result set has exactly one column,
        // QueryAsync maps each row to a string, if it
        // had more columns we would create a class to
        // represent that row's data in C#
        var storageKeys = await connection.QueryAsync<string>(
            "dbo.GetAllArtworkImageStorageKeys",
            commandType: CommandType.StoredProcedure
        );

        return [.. storageKeys];
    }

    public async Task<ImageAttachResult> AttachPrimaryImageToImagelessArtworkByNameAsync(
        ArtworkTypeId typeId,
        ArtworkName artworkName,
        ArtworkImage image
    )
    {
        await using var connection = connectionFactory.Create();

        try
        {
            await using var results = await connection.QueryMultipleAsync(
                "dbo.AttachPrimaryImageToImagelessArtworkByName",
                new
                {
                    ArtworkTypeId = typeId.Value,
                    ArtworkName = artworkName.Value,
                    image.StorageKey,
                    image.OriginalFileName,
                    image.Width,
                    image.Height,
                    image.BlurDataUri,
                },
                commandType: CommandType.StoredProcedure
            );

            var matchType = await results.ReadSingleAsync<ArtworkNameMatchType>();
            var artworkIds = await results.ReadAsync<int>();

            return new ImageAttachResult(matchType, [.. artworkIds.Select(id => new ArtworkId(id))]);
        }
        catch (SqlException exception) when (SqlErrors.IsThrown(exception, SqlErrorNumbers.ArtworkTypeNoLongerExists))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    // the names must be distinct ignoring case, or the table-valued parameter's primary key rejects them
    public async Task<Dictionary<ArtworkName, ArtworkNameMatch>> GetArtworkNameMatchesAsync(
        ArtworkTypeId typeId,
        IReadOnlyCollection<ArtworkName> artworkNames
    )
    {
        var names = new DataTable();
        // must match dbo.ArtworkNameList
        names.Columns.Add("Name", typeof(string));

        foreach (var artworkName in artworkNames)
        {
            names.Rows.Add(artworkName.Value);
        }

        await using var connection = connectionFactory.Create();

        try
        {
            await using var results = await connection.QueryMultipleAsync(
                "dbo.GetArtworkNameMatches",
                new
                {
                    ArtworkTypeId = typeId.Value,
                    ArtworkNames = names.AsTableValuedParameter("dbo.ArtworkNameList"),
                },
                commandType: CommandType.StoredProcedure
            );

            var matchTypes = await results.ReadAsync<NameMatchTypeRow>();
            var matchedArtworks = await results.ReadAsync<NameArtworkRow>();

            // ToLookup groups rows by a key, like a dictionary whose values are lists;
            // a missing key gives an empty list rather than throwing
            var artworkIdsByName = matchedArtworks.ToLookup(row => row.Name, row => new ArtworkId(row.ArtworkId));

            // the procedure returns each name exactly as it was sent, so plain string keys line up
            return matchTypes.ToDictionary(
                row => new ArtworkName(row.Name),
                row => new ArtworkNameMatch(row.MatchType, [.. artworkIdsByName[row.Name]])
            );
        }
        catch (SqlException exception) when (SqlErrors.IsThrown(exception, SqlErrorNumbers.ArtworkTypeNoLongerExists))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
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
