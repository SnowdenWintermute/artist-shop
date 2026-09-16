namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Commerce;
using Dapper;

public class ProductTypeRepository(SqlConnectionFactory connectionFactory)
{
    public async Task<List<ProductType>> GetAllAsync()
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<ProductTypeRow>(
            "dbo.GetProductTypes",
            commandType: CommandType.StoredProcedure
        );
        return
        [
            .. rows.Select(row => new ProductType(
                new ProductTypeId(row.Id),
                new ProductTypeName(row.Name)
            )),
        ];
    }

    private sealed class ProductTypeRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
