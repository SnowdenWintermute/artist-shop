namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Microsoft.Data.SqlClient;

public class ArtworkTypeRepository(SqlConnectionFactory connectionFactory)
{
    private const string UniqueNameConstraint = "Unique_ArtworkTypes_Name";

    // UpdateArtworkType THROWs this when the type was deleted while the page was open
    private const int ArtworkTypeNoLongerExists = 50013;

    // DeleteArtworkType THROWs this when artworks were added while the page was open
    private const int ArtworkTypeInUse = 50014;

    public async Task<List<ArtworkType>> GetAllAsync()
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<ArtworkTypeRow>(
            "dbo.GetArtworkTypes",
            commandType: CommandType.StoredProcedure
        );
        return
        [
            .. rows.Select(row => new ArtworkType(
                new ArtworkTypeId(row.Id),
                new ArtworkTypeName(row.Name)
            )),
        ];
    }

    public async Task<ArtworkTypeWithFields?> GetAsync(ArtworkTypeId id)
    {
        await using var connection = connectionFactory.Create();

        await using var results = await connection.QueryMultipleAsync(
            "dbo.GetArtworkType",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
        );

        var row = await results.ReadSingleOrDefaultAsync<ArtworkTypeRow>();

        if (row is null)
        {
            return null;
        }

        // Dapper converts an int column straight into an enum
        var fields = await results.ReadAsync<ArtworkField>();

        return new ArtworkTypeWithFields(
            new ArtworkTypeId(row.Id),
            new ArtworkTypeName(row.Name),
            [.. fields.Order()]
        );
    }

    public async Task<ArtworkTypeArtworkCounts> CountArtworksAsync(ArtworkTypeId id)
    {
        await using var connection = connectionFactory.Create();

        // Dapper matches the columns to the record's constructor parameters by name
        return await connection.QuerySingleAsync<ArtworkTypeArtworkCounts>(
            "dbo.CountArtworkTypeArtworks",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<ArtworkTypeId> AddAsync(ArtworkTypeName name, IEnumerable<ArtworkField> fields)
    {
        await using var connection = connectionFactory.Create();
        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "dbo.AddArtworkType",
                new { Name = name.Value, ArtworkFieldIds = CreateFieldIdList(fields) },
                commandType: CommandType.StoredProcedure
            );

            return new ArtworkTypeId(id);
        }
        catch (SqlException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    public async Task UpdateAsync(
        ArtworkTypeId id,
        ArtworkTypeName name,
        IEnumerable<ArtworkField> fields
    )
    {
        await using var connection = connectionFactory.Create();
        try
        {
            await connection.ExecuteAsync(
                "dbo.UpdateArtworkType",
                new
                {
                    Id = id.Value,
                    Name = name.Value,
                    ArtworkFieldIds = CreateFieldIdList(fields),
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
            when (SqlErrors.IsThrown(exception, ArtworkTypeNoLongerExists))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(ArtworkTypeId id)
    {
        await using var connection = connectionFactory.Create();
        try
        {
            await connection.ExecuteAsync(
                "dbo.DeleteArtworkType",
                new { Id = id.Value },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (SqlException exception) when (SqlErrors.IsThrown(exception, ArtworkTypeInUse))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    private static SqlMapper.ICustomQueryParameter CreateFieldIdList(
        IEnumerable<ArtworkField> fields
    ) => IdListParameter.Create(fields.Select(field => (int)field));

    private sealed class ArtworkTypeRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
