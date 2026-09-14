namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;

public class ShopItemTypeRepository(SqlConnectionFactory connectionFactory)
{
    public async Task<List<ShopItemType>> GetAllAsync()
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<ShopItemTypeRow>(
            "dbo.GetShopItemTypes",
            commandType: CommandType.StoredProcedure
        );
        return
        [
            .. rows.Select(row => new ShopItemType(
                new ShopItemTypeId(row.Id),
                new ShopItemTypeName(row.Name)
            )),
        ];
    }

    private sealed class ShopItemTypeRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
