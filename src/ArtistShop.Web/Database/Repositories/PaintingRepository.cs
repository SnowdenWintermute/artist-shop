namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using Dapper;

public class PaintingRepository(SqlConnectionFactory connectionFactory)
{
    private static DataTable CreateImageDataTable(Painting painting)
    {
        var images = new DataTable();
        images.Columns.Add("Path", typeof(string));
        images.Columns.Add("SortOrder", typeof(int));
        images.Columns.Add("IsPrimary", typeof(bool));

        for (var i = 0; i < painting.ImageRelativeUrls.Count; i += 1)
        {
            images.Rows.Add(painting.ImageRelativeUrls[i], i, i == painting.MainImageIndex);
        }

        return images;
    }

    public async Task<int> AddAsync(Painting painting)
    {
        var images = CreateImageDataTable(painting);

        await using var connection = connectionFactory.Create();

        return await connection.QuerySingleAsync<int>(
            "dbo.AddPainting",
            new
            {
                Name = painting.Name.Value,
                Slug = painting.Slug.Value,
                painting.Price,
                painting.Stock,
                painting.DatePainted,
                painting.Description,
                WidthCm = painting.Dimensions?.Width,
                HeightCm = painting.Dimensions?.Height,
                Images = images.AsTableValuedParameter("dbo.ShopItemImageList"),
            },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<Painting?> GetBySlugAsync(string slug)
    {
        await using var connection = connectionFactory.Create();

        await using var results = await connection.QueryMultipleAsync(
            "dbo.GetPaintingBySlug",
            new { Slug = slug },
            commandType: CommandType.StoredProcedure
        );

        var row = await results.ReadSingleOrDefaultAsync<PaintingRow>();

        if (row is null)
        {
            return null;
        }

        var images = (await results.ReadAsync<ImageRow>()).ToList();

        var dimensions = row is { WidthCm: decimal width, HeightCm: decimal height }
            ? new DimensionsCentimeters(new Dimensions(width, height))
            : null;

        var painting = new Painting(
            row.Id,
            row.Name,
            row.Slug,
            row.Price,
            row.Stock,
            row.DatePainted,
            images.Select(image => image.Path),
            dimensions,
            row.Description,
            seriesIds: null,
            mediums: null,
            supports: null
        )
        {
            MainImageIndex = Math.Max(images.FindIndex(image => image.IsPrimary), 0),
        };

        return painting;
    }

    private sealed class PaintingRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public required decimal Price { get; init; }
        public required int Stock { get; init; }
        public required DateOnly DatePainted { get; init; }
        public string? Description { get; init; }
        public decimal? WidthCm { get; init; }
        public decimal? HeightCm { get; init; }
    }

    private sealed class ImageRow
    {
        public required string Path { get; init; }
        public required bool IsPrimary { get; init; }
    }
}
