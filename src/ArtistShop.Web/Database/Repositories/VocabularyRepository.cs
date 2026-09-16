namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;
using Dapper;
using Microsoft.Data.SqlClient;

public class VocabularyRepository(SqlConnectionFactory connectionFactory)
{
    private const string UniqueNameConstraint = "Unique_Vocabularies_Name";

    // UpdateVocabulary THROWs this when the vocabulary was deleted while the page was open
    private const int VocabularyNoLongerExists = 50002;

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

    public async Task<List<Vocabulary>> GetAllWithoutArtworkTypesAsync()
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<VocabularyRow>(
            "dbo.GetVocabulariesWithoutArtworkTypes",
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

    public async Task<List<VocabularyWithTerms>> GetAllWithTermsForArtworkTypeAsync(
        ArtworkTypeId artworkTypeId
    )
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<VocabularyWithTermRow>(
            "dbo.GetVocabulariesWithTermsForArtworkType",
            new { ArtworkTypeId = artworkTypeId.Value },
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
        IEnumerable<ArtworkTypeId> artworkTypeIds
    )
    {
        var artworkTypeIdList = IdListParameter.Create(artworkTypeIds.Select(id => id.Value));

        await using var connection = connectionFactory.Create();
        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "dbo.AddVocabulary",
                new { Name = name.Value, ArtworkTypeIds = artworkTypeIdList },
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

    public async Task<VocabularyWithArtworkTypes?> GetAsync(VocabularyId id)
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

        var artworkTypeIds = await results.ReadAsync<int>();

        return new VocabularyWithArtworkTypes(
            new VocabularyId(row.Id),
            new VocabularyName(row.Name),
            [.. artworkTypeIds.Select(artworkTypeId => new ArtworkTypeId(artworkTypeId))]
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
        var artworkTypeIdList = IdListParameter.Create(
            artworkTypeIds.Select(artworkTypeId => artworkTypeId.Value)
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
                    ArtworkTypeIds = artworkTypeIdList,
                },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (SqlException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
        catch (SqlException exception)
            when (SqlErrors.IsThrown(exception, VocabularyNoLongerExists))
        {
            throw new CatalogChangedException(exception.Message, exception);
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

    // @QUESTION: Why is artwork count on vocabulary repo?
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
