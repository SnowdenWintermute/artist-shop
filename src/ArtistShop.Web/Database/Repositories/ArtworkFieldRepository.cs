namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class ArtworkFieldRepository(SiteDatabase database)
{
    public async Task<List<ArtworkFieldDefinition>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ArtworkFieldRow>(
            "SELECT * FROM get_artwork_fields()"
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
