namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;
using Dapper;
using Microsoft.Data.SqlClient;

public class VocabularyRepository(SqlConnectionFactory connectionFactory)
{
    private const string UniqueNameConstraint = "Unique_Vocabularies_Name";

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

    public async Task<List<Vocabulary>> GetAllWithoutShopItemTypesAsync()
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<VocabularyRow>(
            "dbo.GetVocabulariesWithoutShopItemTypes",
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

    public async Task<List<VocabularyWithTerms>> GetAllWithTermsForShopItemTypeAsync(
        ShopItemTypeId shopItemTypeId
    )
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<VocabularyWithTermRow>(
            "dbo.GetVocabulariesWithTermsForShopItemType",
            new { ShopItemTypeId = shopItemTypeId.Value },
            commandType: CommandType.StoredProcedure
        );

        return
        [
            .. rows.GroupBy(row => (row.Id, row.Name))
                .Select(group => new VocabularyWithTerms(
                    new VocabularyId(group.Key.Id),
                    new VocabularyName(group.Key.Name),
                    [
                        .. group
                            .Where(row => row.TermId is not null)
                            .Select(row => new VocabularyTerm(
                                new VocabularyTermId(Unwrap.Value(row.TermId)),
                                new VocabularyTermName(Unwrap.Value(row.TermName)),
                                new VocabularyId(row.Id),
                                new VocabularyName(row.Name)
                            )),
                    ]
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
        catch (SqlException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
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
            [.. shopItemTypeIds.Select(shopItemTypeId => new ShopItemTypeId(shopItemTypeId))]
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
            [
                .. shopItemCounts.Select(row => new ShopItemTypeUsage(
                    new ShopItemTypeId(row.ShopItemTypeId),
                    row.ShopItemCount
                )),
            ]
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
        catch (SqlException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
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

    private sealed class VocabularyWithTermRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public int? TermId { get; init; }
        public string? TermName { get; init; }
    }

    private sealed class VocabularyRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
