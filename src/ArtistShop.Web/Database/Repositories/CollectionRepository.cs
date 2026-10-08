namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class CollectionRepository(SiteDatabase database)
{
    private const string UniqueNameConstraint = "unique_collections_name";
    private const string UniqueSlugConstraint = "unique_collections_slug";

    // the same name also means the same slug, and which constraint Postgres reports first isn't
    // something to rely on, so both mean "name taken"
    private static bool IsNameTaken(PostgresException exception) =>
        SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint)
        || SqlErrors.IsUniqueConstraintViolation(exception, UniqueSlugConstraint);

    public async Task<List<Collection>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<CollectionRow>("SELECT * FROM get_all_collections()");

        return
        [
            .. rows.Select(row => new Collection(
                new CollectionId(row.Id),
                new CollectionName(row.Name),
                new CollectionSlug(row.Slug)
            )),
        ];
    }

    // the artist sees every collection, empty ones included, so they can fill them
    public Task<List<CollectionWithCover>> GetAllWithCoversAsync() =>
        GetWithCoversAsync(onlyWorksWithImages: false);

    // a visitor sees only what there is something to look at in, and the counts follow the
    // same rule, so a card can't promise more than the collection page shows
    public Task<List<CollectionWithCover>> GetVisibleWithCoversAsync() =>
        GetWithCoversAsync(onlyWorksWithImages: true);

    public async Task<Collection?> GetBySlugAsync(string slug)
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleOrDefaultAsync<CollectionRow>(
            "SELECT * FROM get_collection_by_slug(@Slug)",
            new { Slug = slug }
        );

        return row is null
            ? null
            : new Collection(new CollectionId(row.Id), new CollectionName(row.Name), new CollectionSlug(row.Slug));
    }

    // the collection alone, for a page that shows its name but not its works
    public async Task<Collection?> GetByIdAsync(CollectionId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleOrDefaultAsync<CollectionRow>(
            "SELECT * FROM get_collection(@Id)",
            new { Id = id.Value }
        );

        return row is null
            ? null
            : new Collection(new CollectionId(row.Id), new CollectionName(row.Name), new CollectionSlug(row.Slug));
    }

    private async Task<List<CollectionWithCover>> GetWithCoversAsync(bool onlyWorksWithImages)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<CollectionWithCoverRow>(
            "SELECT * FROM get_collections_with_covers(@OnlyWorksWithImages)",
            new { OnlyWorksWithImages = onlyWorksWithImages }
        );

        return
        [
            .. rows.Select(row => new CollectionWithCover(
                new CollectionId(row.Id),
                new CollectionName(row.Name),
                new CollectionSlug(row.Slug),
                row.WorkCount,
                row
                    is {
                        CoverStorageKey: string storageKey,
                        CoverWidth: int width,
                        CoverHeight: int height
                    }
                    ? new WorkImage(
                        storageKey,
                        row.CoverOriginalFileName,
                        width,
                        height,
                        row.CoverBlurDataUri
                    )
                    : null
            )),
        ];
    }

    public async Task<CollectionWithWorks?> GetAsync(CollectionId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_collection(@Id);
            SELECT * FROM get_collection_works(@Id);
            """,
            new { Id = id.Value }
        );

        var row = await results.ReadSingleOrDefaultAsync<CollectionRow>();

        if (row is null)
        {
            return null;
        }

        var works = await results.ReadAsync<CollectionWorkRow>();

        return new CollectionWithWorks(
            new CollectionId(row.Id),
            new CollectionName(row.Name),
            new CollectionSlug(row.Slug),
            [
                .. works.Select(work => new CollectionWork(
                    new WorkId(work.Id),
                    new WorkName(work.Name),
                    new WorkTypeName(work.WorkTypeName),
                    work.IsCover,
                    work
                        is { StorageKey: string storageKey, Width: int width, Height: int height }
                        ? new WorkImage(
                            storageKey,
                            work.OriginalFileName,
                            width,
                            height,
                            work.BlurDataUri
                        )
                        : null
                )),
            ]
        );
    }

    // runs on the caller's transaction, so the collections and whatever needed them are saved together
    // or not at all. Returns each new collection's id by the name it was asked for.
    // A name another collection already has arrives here as ChangedSincePageLoadException: the caller checked
    // the names it had, and another admin adding one since is a change it should look at again
    public static async Task<Dictionary<string, CollectionId>> AddManyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<CollectionName> names
    )
    {
        var distinctNames = names
            .Select(name => name.Value)
            .Distinct(DatabaseCollationComparer.Instance)
            .ToList();

        if (distinctNames.Count == 0)
        {
            return new Dictionary<string, CollectionId>(DatabaseCollationComparer.Instance);
        }

        CollectionNameAndSlug[] collections =
        [
            .. distinctNames.Select(name => new CollectionNameAndSlug(
                name,
                CollectionSlug.FromName(name).Value
            )),
        ];

        try
        {
            var rows = await connection.QueryAsync<AddedCollectionRow>(
                "SELECT * FROM add_many_collections(@Collections)",
                new { Collections = collections },
                transaction
            );

            return rows.ToDictionary(
                row => row.Name,
                row => new CollectionId(row.Id),
                DatabaseCollationComparer.Instance
            );
        }
        catch (PostgresException exception) when (IsNameTaken(exception))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task<CollectionId> AddAsync(CollectionName name, CollectionSlug slug)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "SELECT add_collection(@Name, @Slug)",
                new { Name = name.Value, Slug = slug.Value }
            );

            return new CollectionId(id);
        }
        catch (PostgresException exception) when (IsNameTaken(exception))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    public async Task RenameAsync(CollectionId id, CollectionName name, CollectionSlug slug)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT rename_collection(@Id, @Name, @Slug)",
                new
                {
                    Id = id.Value,
                    Name = name.Value,
                    Slug = slug.Value,
                }
            );
        }
        catch (PostgresException exception) when (IsNameTaken(exception))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.CollectionNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task ReorderAsync(IReadOnlyList<CollectionId> ids)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT reorder_collections(@CollectionIds)",
                new { CollectionIds = (int[])[.. ids.Select(id => id.Value)] }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.CollectionChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(CollectionId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync("SELECT delete_collection(@Id)", new { Id = id.Value });
    }

    public async Task ReorderWorksAsync(CollectionId id, IReadOnlyList<WorkId> workIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT reorder_collection_works(@CollectionId, @WorkIds)",
                new
                {
                    CollectionId = id.Value,
                    WorkIds = (int[])[.. workIds.Select(workId => workId.Value)],
                }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.WorksChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task SetCoverAsync(CollectionId id, WorkId workId)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT set_collection_cover(@CollectionId, @WorkId)",
                new { CollectionId = id.Value, WorkId = workId.Value }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.WorkNoLongerInCollection)
                || SqlErrors.IsThrown(exception, SqlStates.WorkHasNoImage)
            )
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task ClearCoverAsync(CollectionId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "SELECT clear_collection_cover(@CollectionId)",
            new { CollectionId = id.Value }
        );
    }

    // at the end of the collection, in the order given
    public async Task AddWorksAsync(CollectionId id, IReadOnlyList<WorkId> workIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT add_works_to_collection(@CollectionId, @WorkIds)",
                new
                {
                    CollectionId = id.Value,
                    WorkIds = (int[])[.. workIds.Select(workId => workId.Value)],
                }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.CollectionNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task RemoveWorksAsync(CollectionId id, IEnumerable<WorkId> workIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "SELECT remove_works_from_collection(@CollectionId, @WorkIds)",
            new
            {
                CollectionId = id.Value,
                WorkIds = (int[])[.. workIds.Select(workId => workId.Value)],
            }
        );
    }

    private sealed class AddedCollectionRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }

    private sealed class CollectionRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
    }

    private sealed class CollectionWithCoverRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public required int WorkCount { get; init; }
        public string? CoverStorageKey { get; init; }
        public string? CoverOriginalFileName { get; init; }
        public int? CoverWidth { get; init; }
        public int? CoverHeight { get; init; }
        public string? CoverBlurDataUri { get; init; }
    }

    private sealed class CollectionWorkRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string WorkTypeName { get; init; }
        public required bool IsCover { get; init; }
        public string? StorageKey { get; init; }
        public string? OriginalFileName { get; init; }
        public int? Width { get; init; }
        public int? Height { get; init; }
        public string? BlurDataUri { get; init; }
    }
}
