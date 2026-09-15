namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Microsoft.Data.SqlClient;

public class VocabularyRepository(SqlConnectionFactory connectionFactory)
{
    // SQL Server's error number for a violated PRIMARY KEY or UNIQUE constraint
    private const int UniqueConstraintViolation = 2627;

    private static bool IsDuplicateName(SqlException exception) =>
        exception.Number == UniqueConstraintViolation
        && exception.Message.Contains("Unique_Vocabularies_Name");

    public async Task<List<Vocabulary>> GetAllAsync()
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<VocabularyRow>(
            "dbo.GetVocabularies",
            commandType: CommandType.StoredProcedure
        );

        return
        [
            .. rows.Select(row => new Vocabulary(
                new VocabularyId(row.Id),
                new VocabularyName(row.Name)
            )),
        ];
    }

    public async Task<VocabularyId> AddAsync(
        VocabularyName name,
        IEnumerable<ShopItemTypeId> shopItemTypeIds
    )
    {
        var shopItemTypeIdList = IdListParameter.Create(shopItemTypeIds.Select(id => id.Value));

        await using var connection = connectionFactory.Create();
        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "dbo.AddVocabulary",
                new { Name = name.Value, ShopItemTypeIds = shopItemTypeIdList },
                commandType: CommandType.StoredProcedure
            );

            return new VocabularyId(id);
        }
        catch (SqlException exception) when (IsDuplicateName(exception))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    public async Task<VocabularyWithShopItemTypes?> GetAsync(VocabularyId id)
    {
        await using var connection = connectionFactory.Create();

        await using var results = await connection.QueryMultipleAsync(
            "dbo.GetVocabulary",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
        );

        var row = await results.ReadSingleOrDefaultAsync<VocabularyRow>();

        if (row is null)
        {
            return null;
        }

        var shopItemTypeIds = await results.ReadAsync<int>();

        return new VocabularyWithShopItemTypes(
            new VocabularyId(row.Id),
            new VocabularyName(row.Name),
            shopItemTypeIds.Select(shopItemTypeId => new ShopItemTypeId(shopItemTypeId)).ToHashSet()
        );
    }

    public async Task<VocabularyUsage> CountUsageAsync(VocabularyId id)
    {
        await using var connection = connectionFactory.Create();

        await using var results = await connection.QueryMultipleAsync(
            "dbo.CountVocabularyUsage",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
        );

        var termCount = await results.ReadSingleAsync<int>();
        var shopItemCounts = await results.ReadAsync<ShopItemCountRow>();

        return new VocabularyUsage(
            termCount,
            shopItemCounts.ToDictionary(
                row => new ShopItemTypeId(row.ShopItemTypeId),
                row => row.ShopItemCount
            )
        );
    }

    public async Task UpdateAsync(
        VocabularyId id,
        VocabularyName name,
        IEnumerable<ShopItemTypeId> shopItemTypeIds
    )
    {
        var shopItemTypeIdList = IdListParameter.Create(
            shopItemTypeIds.Select(shopItemTypeId => shopItemTypeId.Value)
        );

        await using var connection = connectionFactory.Create();
        try
        {
            // ExecuteAsync: for procedures that return no result set
            await connection.ExecuteAsync(
                "dbo.UpdateVocabulary",
                new
                {
                    Id = id.Value,
                    Name = name.Value,
                    ShopItemTypeIds = shopItemTypeIdList,
                },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (SqlException exception) when (IsDuplicateName(exception))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    public async Task DeleteAsync(VocabularyId id)
    {
        await using var connection = connectionFactory.Create();

        await connection.ExecuteAsync(
            "dbo.DeleteVocabulary",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
        );
    }

    // @QUESTION: Why is shop item count on vocabulary repo?
    private sealed class ShopItemCountRow
    {
        public required int ShopItemTypeId { get; init; }
        public required int ShopItemCount { get; init; }
    }

    private sealed class VocabularyRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
