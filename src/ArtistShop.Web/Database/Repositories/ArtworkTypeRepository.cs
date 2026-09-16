namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;

public class ArtworkTypeRepository(SqlConnectionFactory connectionFactory)
{
    public async Task<List<ArtworkType>> GetAllAsync()
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<ArtworkTypeRow>(
            "dbo.GetArtworkTypes",
            commandType: CommandType.StoredProcedure
        );
        return
        [
            .. rows.Select(row => new ArtworkType(
                new ArtworkTypeId(row.Id),
                new ArtworkTypeName(row.Name)
            )),
        ];
    }

    public async Task<List<ArtworkField>> GetFieldsAsync(ArtworkTypeId artworkTypeId)
    {
        await using var connection = connectionFactory.Create();

        // Dapper converts an int column straight into an enum
        var fields = await connection.QueryAsync<ArtworkField>(
            "dbo.GetArtworkTypeFields",
            new { ArtworkTypeId = artworkTypeId.Value },
            commandType: CommandType.StoredProcedure
        );
        return [.. fields];
    }

    private sealed class ArtworkTypeRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
