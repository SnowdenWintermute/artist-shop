namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class WorkTypeRepository(SiteDatabase database)
{
    private const string UniqueNameConstraint = "unique_work_types_name";

    public async Task<List<WorkType>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<WorkTypeRow>("SELECT * FROM get_work_types()");
        return
        [
            .. rows.Select(row => new WorkType(
                new WorkTypeId(row.Id),
                new WorkTypeName(row.Name)
            )),
        ];
    }

    public async Task<WorkTypeWithFields?> GetAsync(WorkTypeId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        // Npgsql returns one result set per statement
        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_work_type(@Id);
            SELECT * FROM get_work_type_field_ids(@Id);
            """,
            new { Id = id.Value }
        );

        var row = await results.ReadSingleOrDefaultAsync<WorkTypeRow>();

        if (row is null)
        {
            return null;
        }

        // Dapper converts an int column straight into an enum
        var fields = await results.ReadAsync<WorkField>();

        return new WorkTypeWithFields(
            new WorkTypeId(row.Id),
            new WorkTypeName(row.Name),
            [.. fields.Order()]
        );
    }

    public async Task<WorkTypeWorkCounts> CountWorksAsync(WorkTypeId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        // Dapper matches the columns to the record's constructor parameters by name
        return await connection.QuerySingleAsync<WorkTypeWorkCounts>(
            "SELECT * FROM count_work_type_works(@Id)",
            new { Id = id.Value }
        );
    }

    public async Task<WorkTypeId> AddAsync(
        WorkTypeName name,
        IEnumerable<WorkField> fields
    )
    {
        await using var connection = await database.OpenConnectionAsync();
        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "SELECT add_work_type(@Name, @WorkFieldIds)",
                new { Name = name.Value, WorkFieldIds = CreateFieldIdList(fields) }
            );

            return new WorkTypeId(id);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    public async Task UpdateAsync(
        WorkTypeId id,
        WorkTypeName name,
        IEnumerable<WorkField> fields
    )
    {
        await using var connection = await database.OpenConnectionAsync();
        try
        {
            await connection.ExecuteAsync(
                "SELECT update_work_type(@Id, @Name, @WorkFieldIds)",
                new
                {
                    Id = id.Value,
                    Name = name.Value,
                    WorkFieldIds = CreateFieldIdList(fields),
                }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.WorkTypeNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(WorkTypeId id)
    {
        await using var connection = await database.OpenConnectionAsync();
        try
        {
            await connection.ExecuteAsync("SELECT delete_work_type(@Id)", new { Id = id.Value });
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.WorkTypeInUse))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    // Npgsql sends an int[] as a Postgres int[]
    private static int[] CreateFieldIdList(IEnumerable<WorkField> fields) =>
        [.. fields.Select(field => (int)field)];

    private sealed class WorkTypeRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
