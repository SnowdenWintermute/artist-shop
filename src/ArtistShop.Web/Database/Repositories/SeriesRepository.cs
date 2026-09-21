namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Npgsql;

public class SeriesRepository(NpgsqlDataSource dataSource)
{
    private const string UniqueNameConstraint = "Unique_Series_Name";
    private const string UniqueSlugConstraint = "Unique_Series_Slug";

    // the same name also means the same slug, and which constraint SQL Server reports first isn't
    // defined, so both mean "name taken"
    private static bool IsNameTaken(PostgresException exception) =>
        SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint)
        || SqlErrors.IsUniqueConstraintViolation(exception, UniqueSlugConstraint);

    public async Task<List<Series>> GetAllAsync()
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<SeriesRow>(
            "dbo.GetAllSeries",
            commandType: CommandType.StoredProcedure
        );

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
        await using var connection = dataSource.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<SeriesRow>(
            "dbo.GetSeriesBySlug",
            new { Slug = slug },
            commandType: CommandType.StoredProcedure
        );

        return row is null
            ? null
            : new Series(new SeriesId(row.Id), new SeriesName(row.Name), new SeriesSlug(row.Slug));
    }

    private async Task<List<SeriesWithCover>> GetWithCoversAsync(bool onlyArtworksWithImages)
    {
        await using var connection = dataSource.CreateConnection();

        var rows = await connection.QueryAsync<SeriesWithCoverRow>(
            "dbo.GetSeriesWithCovers",
            new { OnlyArtworksWithImages = onlyArtworksWithImages },
            commandType: CommandType.StoredProcedure
        );

        return
        [
            .. rows.Select(row => new SeriesWithCover(
                new SeriesId(row.Id),
                new SeriesName(row.Name),
                new SeriesSlug(row.Slug),
                row.ArtworkCount,
                row is { CoverStorageKey: string storageKey, CoverWidth: int width, CoverHeight: int height }
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
        await using var connection = dataSource.CreateConnection();

        await using var results = await connection.QueryMultipleAsync(
            "dbo.GetSeries",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
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
                    artwork is { StorageKey: string storageKey, Width: int width, Height: int height }
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
    // A name another series already has arrives here as CatalogChangedException: the caller checked
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

        var series = new DataTable();
        // must match dbo.SeriesNameAndSlugList
        series.Columns.Add("Name", typeof(string));
        series.Columns.Add("Slug", typeof(string));

        foreach (var name in distinctNames)
        {
            series.Rows.Add(name, SeriesSlug.FromName(name).Value);
        }

        try
        {
            var rows = await connection.QueryAsync<AddedSeriesRow>(
                "dbo.AddManySeries",
                new { Series = series.AsTableValuedParameter("dbo.SeriesNameAndSlugList") },
                transaction,
                commandType: CommandType.StoredProcedure
            );

            return rows.ToDictionary(
                row => row.Name,
                row => new SeriesId(row.Id),
                DatabaseCollationComparer.Instance
            );
        }
        catch (PostgresException exception) when (IsNameTaken(exception))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task<SeriesId> AddAsync(SeriesName name, SeriesSlug slug)
    {
        await using var connection = dataSource.CreateConnection();

        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "dbo.AddSeries",
                new { Name = name.Value, Slug = slug.Value },
                commandType: CommandType.StoredProcedure
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
        await using var connection = dataSource.CreateConnection();

        try
        {
            await connection.ExecuteAsync(
                "dbo.RenameSeries",
                new
                {
                    Id = id.Value,
                    Name = name.Value,
                    Slug = slug.Value,
                },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (PostgresException exception) when (IsNameTaken(exception))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.SeriesNoLongerExists))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task ReorderAsync(IReadOnlyList<SeriesId> ids)
    {
        await using var connection = dataSource.CreateConnection();

        try
        {
            await connection.ExecuteAsync(
                "dbo.ReorderSeries",
                new { SeriesIds = IdListParameter.CreateOrdered([.. ids.Select(id => id.Value)]) },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.SeriesChangedSincePageLoad))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(SeriesId id)
    {
        await using var connection = dataSource.CreateConnection();

        await connection.ExecuteAsync(
            "dbo.DeleteSeries",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task ReorderArtworksAsync(SeriesId id, IReadOnlyList<ArtworkId> artworkIds)
    {
        await using var connection = dataSource.CreateConnection();

        try
        {
            await connection.ExecuteAsync(
                "dbo.ReorderSeriesArtworks",
                new
                {
                    SeriesId = id.Value,
                    ArtworkIds = IdListParameter.CreateOrdered(
                        [.. artworkIds.Select(artworkId => artworkId.Value)]
                    ),
                },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworksChangedSincePageLoad))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task SetCoverAsync(SeriesId id, ArtworkId artworkId)
    {
        await using var connection = dataSource.CreateConnection();

        try
        {
            await connection.ExecuteAsync(
                "dbo.SetSeriesCover",
                new { SeriesId = id.Value, ArtworkId = artworkId.Value },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworkNoLongerInSeries)
                || SqlErrors.IsThrown(exception, SqlStates.ArtworkHasNoImage))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task ClearCoverAsync(SeriesId id)
    {
        await using var connection = dataSource.CreateConnection();

        await connection.ExecuteAsync(
            "dbo.ClearSeriesCover",
            new { SeriesId = id.Value },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task RemoveArtworksAsync(SeriesId id, IEnumerable<ArtworkId> artworkIds)
    {
        await using var connection = dataSource.CreateConnection();

        await connection.ExecuteAsync(
            "dbo.RemoveArtworksFromSeries",
            new
            {
                SeriesId = id.Value,
                ArtworkIds = IdListParameter.Create(artworkIds.Select(artworkId => artworkId.Value)),
            },
            commandType: CommandType.StoredProcedure
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
