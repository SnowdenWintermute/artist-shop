namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class WorkFieldRepository(SiteDatabase database)
{
    public async Task<List<WorkFieldDefinition>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<WorkFieldRow>(
            "SELECT * FROM get_work_fields()"
        );

        return
        [
            .. rows.OrderBy(row => row.Id)
                .Select(row => new WorkFieldDefinition(
                    row.Id,
                    row.Name,
                    row.RequiresWorkFieldId
                )),
        ];
    }

    private sealed class WorkFieldRow
    {
        public required WorkField Id { get; init; }
        public required string Name { get; init; }
        public required WorkField RequiresWorkFieldId { get; init; }
    }
}
