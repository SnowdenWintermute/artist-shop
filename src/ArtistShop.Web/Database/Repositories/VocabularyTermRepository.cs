namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class VocabularyTermRepository(NpgsqlDataSource dataSource)
{
    private const string UniqueNameConstraint = "Unique_VocabularyTerms_VocabularyName";

    public async Task<List<VocabularyTermWithUsage>> GetAllWithUsageAsync(VocabularyId vocabularyId)
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<VocabularyTermWithUsageRow>(
            "dbo.GetVocabularyTermsWithUsage",
            new { VocabularyId = vocabularyId.Value },
            commandType: CommandType.StoredProcedure
        );

        return
        [
            .. rows.Select(row => new VocabularyTermWithUsage(
                new VocabularyTermId(row.Id),
                new VocabularyTermName(row.Name),
                row.ArtworkCount
            )),
        ];
    }

    public async Task<VocabularyTermId> AddAsync(VocabularyId vocabularyId, VocabularyTermName name)
    {
        await using var connection = dataSource.CreateConnection();

        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "dbo.AddVocabularyTerm",
                new { VocabularyId = vocabularyId.Value, Name = name.Value },
                commandType: CommandType.StoredProcedure
            );

            return new VocabularyTermId(id);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    public async Task RenameAsync(VocabularyTermId id, VocabularyTermName name)
    {
        await using var connection = dataSource.CreateConnection();

        try
        {
            await connection.ExecuteAsync(
                "dbo.RenameVocabularyTerm",
                new { Id = id.Value, Name = name.Value },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.VocabularyTermNoLongerExists))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(VocabularyTermId id)
    {
        await using var connection = dataSource.CreateConnection();

        await connection.ExecuteAsync(
            "dbo.DeleteVocabularyTerm",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
        );
    }

    private sealed class VocabularyTermWithUsageRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required int ArtworkCount { get; init; }
    }
}
