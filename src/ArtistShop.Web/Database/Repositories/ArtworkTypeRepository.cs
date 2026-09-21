namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class ArtworkTypeRepository(NpgsqlDataSource dataSource)
{
    private const string UniqueNameConstraint = "unique_artwork_types_name";

    public async Task<List<ArtworkType>> GetAllAsync()
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<ArtworkTypeRow>("SELECT * FROM get_artwork_types()");
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
        await using var connection = dataSource.CreateConnection();

        // Npgsql returns one result set per statement
        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_artwork_type(@Id);
            SELECT * FROM get_artwork_type_field_ids(@Id);
            """,
            new { Id = id.Value }
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
        await using var connection = dataSource.CreateConnection();

        // Dapper matches the columns to the record's constructor parameters by name
        return await connection.QuerySingleAsync<ArtworkTypeArtworkCounts>(
            "SELECT * FROM count_artwork_type_artworks(@Id)",
            new { Id = id.Value }
        );
    }

    public async Task<ArtworkTypeId> AddAsync(
        ArtworkTypeName name,
        IEnumerable<ArtworkField> fields
    )
    {
        await using var connection = dataSource.CreateConnection();
        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "SELECT add_artwork_type(@Name, @ArtworkFieldIds)",
                new { Name = name.Value, ArtworkFieldIds = CreateFieldIdList(fields) }
            );

            return new ArtworkTypeId(id);
        }
        catch (PostgresException exception)
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
        await using var connection = dataSource.CreateConnection();
        try
        {
            await connection.ExecuteAsync(
                "SELECT update_artwork_type(@Id, @Name, @ArtworkFieldIds)",
                new
                {
                    Id = id.Value,
                    Name = name.Value,
                    ArtworkFieldIds = CreateFieldIdList(fields),
                }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworkTypeNoLongerExists))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(ArtworkTypeId id)
    {
        await using var connection = dataSource.CreateConnection();
        try
        {
            await connection.ExecuteAsync("SELECT delete_artwork_type(@Id)", new { Id = id.Value });
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworkTypeInUse))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    // Npgsql sends an int[] as a Postgres int[]
    private static int[] CreateFieldIdList(IEnumerable<ArtworkField> fields) =>
        [.. fields.Select(field => (int)field)];

    private sealed class ArtworkTypeRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
