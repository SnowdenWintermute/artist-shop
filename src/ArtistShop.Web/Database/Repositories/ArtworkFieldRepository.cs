namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class ArtworkFieldRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<ArtworkFieldDefinition>> GetAllAsync()
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<ArtworkFieldRow>(
            "dbo.GetArtworkFields",
            commandType: CommandType.StoredProcedure
        );

        return
        [
            .. rows.OrderBy(row => row.Id)
                .Select(row => new ArtworkFieldDefinition(
                    row.Id,
                    row.Name,
                    row.RequiresArtworkFieldId
                )),
        ];
    }

    private sealed class ArtworkFieldRow
    {
        public required ArtworkField Id { get; init; }
        public required string Name { get; init; }
        public required ArtworkField RequiresArtworkFieldId { get; init; }
    }
}
