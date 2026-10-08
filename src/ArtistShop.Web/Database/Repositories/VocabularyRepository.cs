namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;
using Dapper;
using Npgsql;

public class VocabularyRepository(SiteDatabase database)
{
    private const string UniqueNameConstraint = "unique_vocabularies_name";

    public async Task<List<Vocabulary>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<VocabularyRow>("SELECT * FROM get_vocabularies()");

        return [.. rows.Select(ToVocabulary)];
    }

    public async Task<List<Vocabulary>> GetAllWithoutWorkTypesAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<VocabularyRow>(
            "SELECT * FROM get_vocabularies_without_work_types()"
        );

        return [.. rows.Select(ToVocabulary)];
    }

    public Task<List<VocabularyWithTerms>> GetAllWithTermsAsync() =>
        GetAllWithTermsAsync(workTypeId: null);

    public Task<List<VocabularyWithTerms>> GetAllWithTermsForWorkTypeAsync(
        WorkTypeId workTypeId
    ) => GetAllWithTermsAsync(workTypeId.Value);

    private async Task<List<VocabularyWithTerms>> GetAllWithTermsAsync(int? workTypeId)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<VocabularyWithTermRow>(
            "SELECT * FROM get_vocabularies_with_terms(@WorkTypeId)",
            new { WorkTypeId = workTypeId }
        );

        return GroupIntoVocabularies(rows);
    }

    // the rows arrive one per term, with the vocabulary's columns repeated on each
    private static List<VocabularyWithTerms> GroupIntoVocabularies(
        IEnumerable<VocabularyWithTermRow> rows
    ) =>
        [
            .. rows.GroupBy(row => (row.Id, row.Name, row.IsMutuallyExclusive))
                .Select(group => new VocabularyWithTerms(
                    new VocabularyId(group.Key.Id),
                    new VocabularyName(group.Key.Name),
                    group.Key.IsMutuallyExclusive,
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

    public async Task<VocabularyId> AddAsync(
        VocabularyName name,
        bool isMutuallyExclusive,
        IEnumerable<WorkTypeId> workTypeIds
    )
    {
        int[] workTypeIdList = [.. workTypeIds.Select(id => id.Value)];

        await using var connection = await database.OpenConnectionAsync();
        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "SELECT add_vocabulary(@Name, @IsMutuallyExclusive, @WorkTypeIds)",
                new
                {
                    Name = name.Value,
                    IsMutuallyExclusive = isMutuallyExclusive,
                    WorkTypeIds = workTypeIdList,
                }
            );

            return new VocabularyId(id);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    // a vocabulary already named this gains the type rather than being refused
    public async Task<VocabularyId> AddOrLinkAsync(VocabularyName name, WorkTypeId workTypeId)
    {
        await using var connection = await database.OpenConnectionAsync();

        var id = await connection.QuerySingleAsync<int>(
            "SELECT add_or_link_vocabulary(@Name, @WorkTypeId)",
            new { Name = name.Value, WorkTypeId = workTypeId.Value }
        );

        return new VocabularyId(id);
    }

    public async Task<VocabularyWithWorkTypes?> GetAsync(VocabularyId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_vocabulary(@Id);
            SELECT * FROM get_vocabulary_work_type_ids(@Id);
            """,
            new { Id = id.Value }
        );

        var row = await results.ReadSingleOrDefaultAsync<SingleVocabularyRow>();

        if (row is null)
        {
            return null;
        }

        var workTypeIds = await results.ReadAsync<int>();

        return new VocabularyWithWorkTypes(
            new VocabularyId(row.Id),
            new VocabularyName(row.Name),
            row.IsMutuallyExclusive,
            [.. workTypeIds.Select(workTypeId => new WorkTypeId(workTypeId))]
        );
    }

    public async Task<VocabularyUsage> CountUsageAsync(VocabularyId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT count_vocabulary_terms(@Id);
            SELECT * FROM count_vocabulary_works_by_type(@Id);
            """,
            new { Id = id.Value }
        );

        var termCount = await results.ReadSingleAsync<int>();
        var workCounts = await results.ReadAsync<WorkCountRow>();

        return new VocabularyUsage(
            termCount,
            [
                .. workCounts.Select(row => new WorkTypeUsage(
                    new WorkTypeId(row.WorkTypeId),
                    row.WorkCount
                )),
            ]
        );
    }

    // the works that making it mutually exclusive would take its terms off
    public async Task<List<WorkName>> GetWorksWithSeveralTermsAsync(VocabularyId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        var names = await connection.QueryAsync<string>(
            "SELECT * FROM get_vocabulary_works_with_several_terms(@Id)",
            new { Id = id.Value }
        );

        return [.. names.Select(name => new WorkName(name))];
    }

    // making it mutually exclusive removes its terms from every work that has more than one of them
    public async Task UpdateAsync(
        VocabularyId id,
        VocabularyName name,
        bool isMutuallyExclusive,
        IEnumerable<WorkTypeId> workTypeIds
    )
    {
        int[] workTypeIdList = [.. workTypeIds.Select(workTypeId => workTypeId.Value)];

        await using var connection = await database.OpenConnectionAsync();
        try
        {
            // ExecuteAsync: for calls whose result nobody reads
            await connection.ExecuteAsync(
                "SELECT update_vocabulary(@Id, @Name, @IsMutuallyExclusive, @WorkTypeIds)",
                new
                {
                    Id = id.Value,
                    Name = name.Value,
                    IsMutuallyExclusive = isMutuallyExclusive,
                    WorkTypeIds = workTypeIdList,
                }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.VocabularyNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(VocabularyId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync("SELECT delete_vocabulary(@Id)", new { Id = id.Value });
    }

    private static Vocabulary ToVocabulary(VocabularyRow row) =>
        new(new VocabularyId(row.Id), new VocabularyName(row.Name));

    private sealed class WorkCountRow
    {
        public required int WorkTypeId { get; init; }
        public required int WorkCount { get; init; }
    }

    private sealed class VocabularyWithTermRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required bool IsMutuallyExclusive { get; init; }
        public int? TermId { get; init; }
        public string? TermName { get; init; }
    }

    private sealed class SingleVocabularyRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required bool IsMutuallyExclusive { get; init; }
    }

    private sealed class VocabularyRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
