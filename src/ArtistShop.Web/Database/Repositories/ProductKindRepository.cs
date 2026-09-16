namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Commerce;
using Dapper;

public class ProductKindRepository(SqlConnectionFactory connectionFactory)
{
    public async Task<List<ProductKind>> GetAllAsync()
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<ProductKindRow>(
            "dbo.GetProductKinds",
            commandType: CommandType.StoredProcedure
        );
        return
        [
            .. rows.Select(row => new ProductKind(
                new ProductKindId(row.Id),
                new ProductKindName(row.Name)
            )),
        ];
    }

    private sealed class ProductKindRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
