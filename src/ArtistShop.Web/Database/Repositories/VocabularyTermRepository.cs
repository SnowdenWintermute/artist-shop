namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class VocabularyTermRepository(SiteDatabase database)
{
    private const string UniqueNameConstraint = "unique_vocabulary_terms_vocabulary_name";

    public async Task<List<VocabularyTermWithUsage>> GetAllWithUsageAsync(VocabularyId vocabularyId)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<VocabularyTermWithUsageRow>(
            "SELECT * FROM get_vocabulary_terms_with_usage(@VocabularyId)",
            new { VocabularyId = vocabularyId.Value }
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
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "SELECT add_vocabulary_term(@VocabularyId, @Name)",
                new { VocabularyId = vocabularyId.Value, Name = name.Value }
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
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT rename_vocabulary_term(@Id, @Name)",
                new { Id = id.Value, Name = name.Value }
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
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(VocabularyTermId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync("SELECT delete_vocabulary_term(@Id)", new { Id = id.Value });
    }

    private sealed class VocabularyTermWithUsageRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required int ArtworkCount { get; init; }
    }
}
