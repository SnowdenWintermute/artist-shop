namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;
using Microsoft.Data.SqlClient;

public class SeriesRepository(SqlConnectionFactory connectionFactory)
{
    private const string UniqueNameConstraint = "Unique_Series_Name";
    private const string UniqueSlugConstraint = "Unique_Series_Slug";

    // the same name also means the same slug, and which constraint SQL Server reports first isn't
    // defined, so both mean "name taken"
    private static bool IsNameTaken(SqlException exception) =>
        SqlErrors.IsUniqueConstraintViolation(exception, UniqueNameConstraint)
        || SqlErrors.IsUniqueConstraintViolation(exception, UniqueSlugConstraint);

    public async Task<List<Series>> GetAllAsync()
    {
        await using var connection = connectionFactory.Create();

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

    public async Task<List<SeriesWithCover>> GetAllWithCoversAsync()
    {
        await using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<SeriesWithCoverRow>(
            "dbo.GetSeriesWithCovers",
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
        await using var connection = connectionFactory.Create();

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

    public async Task<SeriesId> AddAsync(SeriesName name, SeriesSlug slug)
    {
        await using var connection = connectionFactory.Create();

        try
        {
            var id = await connection.QuerySingleAsync<int>(
                "dbo.AddSeries",
                new { Name = name.Value, Slug = slug.Value },
                commandType: CommandType.StoredProcedure
            );

            return new SeriesId(id);
        }
        catch (SqlException exception) when (IsNameTaken(exception))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
    }

    public async Task RenameAsync(SeriesId id, SeriesName name, SeriesSlug slug)
    {
        await using var connection = connectionFactory.Create();

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
        catch (SqlException exception) when (IsNameTaken(exception))
        {
            throw new NameAlreadyInUseException(name.Value);
        }
        catch (SqlException exception)
            when (SqlErrors.IsThrown(exception, SqlErrorNumbers.SeriesNoLongerExists))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task ReorderAsync(IReadOnlyList<SeriesId> ids)
    {
        await using var connection = connectionFactory.Create();

        try
        {
            await connection.ExecuteAsync(
                "dbo.ReorderSeries",
                new { SeriesIds = IdListParameter.CreateOrdered([.. ids.Select(id => id.Value)]) },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (SqlException exception)
            when (SqlErrors.IsThrown(exception, SqlErrorNumbers.SeriesChangedSincePageLoad))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task DeleteAsync(SeriesId id)
    {
        await using var connection = connectionFactory.Create();

        await connection.ExecuteAsync(
            "dbo.DeleteSeries",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task ReorderArtworksAsync(SeriesId id, IReadOnlyList<ArtworkId> artworkIds)
    {
        await using var connection = connectionFactory.Create();

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
        catch (SqlException exception)
            when (SqlErrors.IsThrown(exception, SqlErrorNumbers.ArtworksChangedSincePageLoad))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task SetCoverAsync(SeriesId id, ArtworkId artworkId)
    {
        await using var connection = connectionFactory.Create();

        try
        {
            await connection.ExecuteAsync(
                "dbo.SetSeriesCover",
                new { SeriesId = id.Value, ArtworkId = artworkId.Value },
                commandType: CommandType.StoredProcedure
            );
        }
        catch (SqlException exception)
            when (SqlErrors.IsThrown(exception, SqlErrorNumbers.ArtworkNoLongerInSeries)
                || SqlErrors.IsThrown(exception, SqlErrorNumbers.ArtworkHasNoImage))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    public async Task ClearCoverAsync(SeriesId id)
    {
        await using var connection = connectionFactory.Create();

        await connection.ExecuteAsync(
            "dbo.ClearSeriesCover",
            new { SeriesId = id.Value },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task RemoveArtworksAsync(SeriesId id, IEnumerable<ArtworkId> artworkIds)
    {
        await using var connection = connectionFactory.Create();

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
