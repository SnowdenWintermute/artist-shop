namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class SeriesRepository(SiteDatabase database)
{
    private const string UniqueNameConstraint = "unique_series_name";
    private const string UniqueSlugConstraint = "unique_series_slug";

    // the same name also means the same slug, and which constraint Postgres reports first isn't
    // something to rely on, so both mean "name taken"
    private static bool IsNameTaken(PostgresException exception) =>
        SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint)
        || SqlErrors.IsUniqueConstraintViolation(exception, UniqueSlugConstraint);

    public async Task<List<Series>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<SeriesRow>("SELECT * FROM get_all_series()");

        return
        [
            .. rows.Select(row => new Series(
                new SeriesId(row.Id),
                new SeriesName(row.Name),
                new SeriesSlug(row.Slug)
            )),
        ];
    }

    // the artist sees every series, empty ones included, so they can fill them
    public Task<List<SeriesWithCover>> GetAllWithCoversAsync() =>
        GetWithCoversAsync(onlyArtworksWithImages: false);

    // a visitor sees only what there is something to look at in, and the counts follow the
    // same rule, so a card can't promise more than the series page shows
    public Task<List<SeriesWithCover>> GetVisibleWithCoversAsync() =>
        GetWithCoversAsync(onlyArtworksWithImages: true);

    public async Task<Series?> GetBySlugAsync(string slug)
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleOrDefaultAsync<SeriesRow>(
            "SELECT * FROM get_series_by_slug(@Slug)",
            new { Slug = slug }
        );

        return row is null
            ? null
            : new Series(new SeriesId(row.Id), new SeriesName(row.Name), new SeriesSlug(row.Slug));
    }

    private async Task<List<SeriesWithCover>> GetWithCoversAsync(bool onlyArtworksWithImages)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<SeriesWithCoverRow>(
            "SELECT * FROM get_series_with_covers(@OnlyArtworksWithImages)",
            new { OnlyArtworksWithImages = onlyArtworksWithImages }
        );

        return
        [
            .. rows.Select(row => new SeriesWithCover(
                new SeriesId(row.Id),
                new SeriesName(row.Name),
                new SeriesSlug(row.Slug),
                row.ArtworkCount,
                row
                    is {
                        CoverStorageKey: string storageKey,
                        CoverWidth: int width,
                        CoverHeight: int height
                    }
                    ? new ArtworkImage(
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

    public async Task<SeriesWithArtworks?> GetAsync(SeriesId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_series(@Id);
            SELECT * FROM get_series_artworks(@Id);
            """,
            new { Id = id.Value }
        );

        var row = await results.ReadSingleOrDefaultAsync<SeriesRow>();

        if (row is null)
        {
            return null;
        }

        var artworks = await results.ReadAsync<SeriesArtworkRow>();

        return new SeriesWithArtworks(
            new SeriesId(row.Id),
            new SeriesName(row.Name),
            new SeriesSlug(row.Slug),
            [
                .. artworks.Select(artwork => new SeriesArtwork(
                    new ArtworkId(artwork.Id),
                    new ArtworkName(artwork.Name),
                    new ArtworkTypeName(artwork.ArtworkTypeName),
                    artwork.IsCover,
                    artwork
                        is { StorageKey: string storageKey, Width: int width, Height: int height }
                        ? new ArtworkImage(
                            storageKey,
                            artwork.OriginalFileName,
                            width,
                            height,
                            artwork.BlurDataUri
                        )
                        : null
                )),
            ]
        );
    }

    // runs on the caller's transaction, so the series and whatever needed them are saved together
    // or not at all. Returns each new series' id by the name it was asked for.
    // A name another series already has arrives here as ChangedSincePageLoadException: the caller checked
    // the names it had, and another admin adding one since is a change it should look at again
    public static async Task<Dictionary<string, SeriesId>> AddManyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<SeriesName> names
    )
    {
        var distinctNames = names
            .Select(name => name.Value)
            .Distinct(DatabaseCollationComparer.Instance)
            .ToList();

        if (distinctNames.Count == 0)
        {
            return new Dictionary<string, SeriesId>(DatabaseCollationComparer.Instance);
        }

        SeriesNameAndSlug[] series =
        [
            .. distinctNames.Select(name => new SeriesNameAndSlug(
                name,
                SeriesSlug.FromName(name).Value
            )),
        ];

        try
        {
            var rows = await connection.QueryAsync<AddedSeriesRow>(
                "SELECT * FROM add_many_series(@Series)",
                new { Series = series },
                transaction
            );

            return rows.ToDictionary(
                row => row.Name,
                row => new SeriesId(row.Id),
                DatabaseCollationComparer.Instance
            );
        }
        catch (PostgresException exception) when (IsNameTaken(exception))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task<SeriesId> AddAsync(SeriesName name, SeriesSlug slug)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "SELECT add_series(@Name, @Slug)",
                new { Name = name.Value, Slug = slug.Value }
            );

            return new SeriesId(id);
        }
        catch (PostgresException exception) when (IsNameTaken(exception))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    public async Task RenameAsync(SeriesId id, SeriesName name, SeriesSlug slug)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT rename_series(@Id, @Name, @Slug)",
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
            when (SqlErrors.IsThrown(exception, SqlStates.SeriesNoLongerExists))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task ReorderAsync(IReadOnlyList<SeriesId> ids)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT reorder_series(@SeriesIds)",
                new { SeriesIds = (int[])[.. ids.Select(id => id.Value)] }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.SeriesChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(SeriesId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync("SELECT delete_series(@Id)", new { Id = id.Value });
    }

    public async Task ReorderArtworksAsync(SeriesId id, IReadOnlyList<ArtworkId> artworkIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT reorder_series_artworks(@SeriesId, @ArtworkIds)",
                new
                {
                    SeriesId = id.Value,
                    ArtworkIds = (int[])[.. artworkIds.Select(artworkId => artworkId.Value)],
                }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworksChangedSincePageLoad))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task SetCoverAsync(SeriesId id, ArtworkId artworkId)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT set_series_cover(@SeriesId, @ArtworkId)",
                new { SeriesId = id.Value, ArtworkId = artworkId.Value }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworkNoLongerInSeries)
                || SqlErrors.IsThrown(exception, SqlStates.ArtworkHasNoImage)
            )
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    public async Task ClearCoverAsync(SeriesId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "SELECT clear_series_cover(@SeriesId)",
            new { SeriesId = id.Value }
        );
    }

    public async Task RemoveArtworksAsync(SeriesId id, IEnumerable<ArtworkId> artworkIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "SELECT remove_artworks_from_series(@SeriesId, @ArtworkIds)",
            new
            {
                SeriesId = id.Value,
                ArtworkIds = (int[])[.. artworkIds.Select(artworkId => artworkId.Value)],
            }
        );
    }

    private sealed class AddedSeriesRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }

    private sealed class SeriesRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
    }

    private sealed class SeriesWithCoverRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public required int ArtworkCount { get; init; }
        public string? CoverStorageKey { get; init; }
        public string? CoverOriginalFileName { get; init; }
        public int? CoverWidth { get; init; }
        public int? CoverHeight { get; init; }
        public string? CoverBlurDataUri { get; init; }
    }

    private sealed class SeriesArtworkRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string ArtworkTypeName { get; init; }
        public required bool IsCover { get; init; }
        public string? StorageKey { get; init; }
        public string? OriginalFileName { get; init; }
        public int? Width { get; init; }
        public int? Height { get; init; }
        public string? BlurDataUri { get; init; }
    }
}
