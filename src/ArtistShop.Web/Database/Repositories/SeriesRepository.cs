namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
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
                row.ShopItemCount,
                row is { CoverRelativePath: string relativePath, CoverWidth: int width, CoverHeight: int height }
                    ? new ShopItemImage(
                        relativePath,
                        row.CoverOriginalFileName,
                        width,
                        height,
                        row.CoverBlurDataUri
                    )
                    : null
            )),
        ];
    }

    public async Task<SeriesWithShopItems?> GetAsync(SeriesId id)
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

        var shopItems = await results.ReadAsync<SeriesShopItemRow>();

        return new SeriesWithShopItems(
            new SeriesId(row.Id),
            new SeriesName(row.Name),
            new SeriesSlug(row.Slug),
            [
                .. shopItems.Select(shopItem => new SeriesShopItem(
                    new ShopItemId(shopItem.Id),
                    new ShopItemName(shopItem.Name),
                    new ShopItemTypeName(shopItem.ShopItemTypeName),
                    shopItem.IsCover,
                    shopItem is { RelativePath: string relativePath, Width: int width, Height: int height }
                        ? new ShopItemImage(
                            relativePath,
                            shopItem.OriginalFileName,
                            width,
                            height,
                            shopItem.BlurDataUri
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

    public async Task ReorderShopItemsAsync(SeriesId id, IReadOnlyList<ShopItemId> shopItemIds)
    {
        await using var connection = connectionFactory.Create();

        await connection.ExecuteAsync(
            "dbo.ReorderSeriesShopItems",
            new
            {
                SeriesId = id.Value,
                ShopItemIds = IdListParameter.CreateOrdered(
                    [.. shopItemIds.Select(shopItemId => shopItemId.Value)]
                ),
            },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task SetCoverAsync(SeriesId id, ShopItemId shopItemId)
    {
        await using var connection = connectionFactory.Create();

        await connection.ExecuteAsync(
            "dbo.SetSeriesCover",
            new { SeriesId = id.Value, ShopItemId = shopItemId.Value },
            commandType: CommandType.StoredProcedure
        );
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

    public async Task RemoveShopItemsAsync(SeriesId id, IEnumerable<ShopItemId> shopItemIds)
    {
        await using var connection = connectionFactory.Create();

        await connection.ExecuteAsync(
            "dbo.RemoveShopItemsFromSeries",
            new
            {
                SeriesId = id.Value,
                ShopItemIds = IdListParameter.Create(shopItemIds.Select(shopItemId => shopItemId.Value)),
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
        public required int ShopItemCount { get; init; }
        public string? CoverRelativePath { get; init; }
        public string? CoverOriginalFileName { get; init; }
        public int? CoverWidth { get; init; }
        public int? CoverHeight { get; init; }
        public string? CoverBlurDataUri { get; init; }
    }

    private sealed class SeriesShopItemRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string ShopItemTypeName { get; init; }
        public required bool IsCover { get; init; }
        public string? RelativePath { get; init; }
        public string? OriginalFileName { get; init; }
        public int? Width { get; init; }
        public int? Height { get; init; }
        public string? BlurDataUri { get; init; }
    }
}
