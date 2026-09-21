namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Commerce;
using Dapper;
using Npgsql;

public class ProductTypeRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<ProductType>> GetAllAsync()
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<ProductTypeRow>("SELECT * FROM get_product_types()");
        return
        [
            .. rows.Select(row => new ProductType(
                new ProductTypeId(row.Id),
                new ProductTypeName(row.Name),
                row.IsDefault
            )),
        ];
    }

    private sealed class ProductTypeRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required bool IsDefault { get; init; }
    }
}
