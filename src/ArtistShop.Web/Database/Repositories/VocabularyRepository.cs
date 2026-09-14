namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Microsoft.Data.SqlClient;

public class VocabularyRepository(SqlConnectionFactory connectionFactory)
{
    // SQL Server's error number for a violated PRIMARY KEY or UNIQUE constraint
    private const int UniqueConstraintViolation = 2627;

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
        catch (SqlException exception)
            when (exception.Number == UniqueConstraintViolation
                && exception.Message.Contains("Unique_Vocabularies_Name")
            )
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    private sealed class VocabularyRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
