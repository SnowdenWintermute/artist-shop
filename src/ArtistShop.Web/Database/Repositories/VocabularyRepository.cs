namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;
using Dapper;
using Npgsql;

public class VocabularyRepository(NpgsqlDataSource dataSource)
{
    private const string UniqueNameConstraint = "unique_vocabularies_name";

    public async Task<List<Vocabulary>> GetAllAsync()
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<VocabularyRow>("SELECT * FROM get_vocabularies()");

        return [.. rows.Select(ToVocabulary)];
    }

    public async Task<List<Vocabulary>> GetAllWithoutArtworkTypesAsync()
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<VocabularyRow>(
            "SELECT * FROM get_vocabularies_without_artwork_types()"
        );

        return [.. rows.Select(ToVocabulary)];
    }

    public Task<List<VocabularyWithTerms>> GetAllWithTermsAsync() =>
        GetAllWithTermsAsync(artworkTypeId: null);

    public Task<List<VocabularyWithTerms>> GetAllWithTermsForArtworkTypeAsync(
        ArtworkTypeId artworkTypeId
    ) => GetAllWithTermsAsync(artworkTypeId.Value);

    private async Task<List<VocabularyWithTerms>> GetAllWithTermsAsync(int? artworkTypeId)
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<VocabularyWithTermRow>(
            "SELECT * FROM get_vocabularies_with_terms(@ArtworkTypeId)",
            new { ArtworkTypeId = artworkTypeId }
        );

        return GroupIntoVocabularies(rows);
    }

    // the rows arrive one per term, with the vocabulary's columns repeated on each
    private static List<VocabularyWithTerms> GroupIntoVocabularies(
        IEnumerable<VocabularyWithTermRow> rows
    ) =>
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

    public async Task<VocabularyId> AddAsync(
        VocabularyName name,
        IEnumerable<ArtworkTypeId> artworkTypeIds
    )
    {
        int[] artworkTypeIdList = [.. artworkTypeIds.Select(id => id.Value)];

        await using var connection = dataSource.CreateConnection();
        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "SELECT add_vocabulary(@Name, @ArtworkTypeIds)",
                new { Name = name.Value, ArtworkTypeIds = artworkTypeIdList }
            );

            return new VocabularyId(id);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    public async Task<VocabularyWithArtworkTypes?> GetAsync(VocabularyId id)
    {
        await using var connection = dataSource.CreateConnection();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_vocabulary(@Id);
            SELECT * FROM get_vocabulary_artwork_type_ids(@Id);
            """,
            new { Id = id.Value }
        );

        var row = await results.ReadSingleOrDefaultAsync<VocabularyRow>();

        if (row is null)
        {
            return null;
        }

        var artworkTypeIds = await results.ReadAsync<int>();

        return new VocabularyWithArtworkTypes(
            new VocabularyId(row.Id),
            new VocabularyName(row.Name),
            [.. artworkTypeIds.Select(artworkTypeId => new ArtworkTypeId(artworkTypeId))]
        );
    }

    public async Task<VocabularyUsage> CountUsageAsync(VocabularyId id)
    {
        await using var connection = dataSource.CreateConnection();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT count_vocabulary_terms(@Id);
            SELECT * FROM count_vocabulary_artworks_by_type(@Id);
            """,
            new { Id = id.Value }
        );

        var termCount = await results.ReadSingleAsync<int>();
        var artworkCounts = await results.ReadAsync<ArtworkCountRow>();

        return new VocabularyUsage(
            termCount,
            [
                .. artworkCounts.Select(row => new ArtworkTypeUsage(
                    new ArtworkTypeId(row.ArtworkTypeId),
                    row.ArtworkCount
                )),
            ]
        );
    }

    public async Task UpdateAsync(
        VocabularyId id,
        VocabularyName name,
        IEnumerable<ArtworkTypeId> artworkTypeIds
    )
    {
        int[] artworkTypeIdList = [.. artworkTypeIds.Select(artworkTypeId => artworkTypeId.Value)];

        await using var connection = dataSource.CreateConnection();
        try
        {
            // ExecuteAsync: for calls whose result nobody reads
            await connection.ExecuteAsync(
                "SELECT update_vocabulary(@Id, @Name, @ArtworkTypeIds)",
                new
                {
                    Id = id.Value,
                    Name = name.Value,
                    ArtworkTypeIds = artworkTypeIdList,
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
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(VocabularyId id)
    {
        await using var connection = dataSource.CreateConnection();

        await connection.ExecuteAsync("SELECT delete_vocabulary(@Id)", new { Id = id.Value });
    }

    private static Vocabulary ToVocabulary(VocabularyRow row) =>
        new(new VocabularyId(row.Id), new VocabularyName(row.Name));

    private sealed class ArtworkCountRow
    {
        public required int ArtworkTypeId { get; init; }
        public required int ArtworkCount { get; init; }
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
