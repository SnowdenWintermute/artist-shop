namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using Dapper;

public class PaintingRepository(SqlConnectionFactory connectionFactory)
{
    private static DataTable CreateImageDataTable(PaintingCatalogAddition paintingCatalogAddition)
    {
        var images = new DataTable();
        images.Columns.Add("RelativePath", typeof(string));
        images.Columns.Add("SortOrder", typeof(int));
        images.Columns.Add("IsPrimary", typeof(bool));
        images.Columns.Add("Width", typeof(int));
        images.Columns.Add("Height", typeof(int));
        images.Columns.Add("BlurDataUri", typeof(string));

        for (var i = 0; i < paintingCatalogAddition.Images.Count; i += 1)
        {
            var image = paintingCatalogAddition.Images[i];
            // must match the Table Value Property dbo.ShopItemImageList
            // parameter order
            images.Rows.Add(
                image.RelativePath,
                i,
                i == paintingCatalogAddition.MainImageIndex,
                image.Width,
                image.Height,
                image.BlurDataUri
            );
        }

        return images;
    }

    // for PaintingSeries, Medium and Support which all are stored
    // as id lists
    private static DataTable CreateIdDataTable(IEnumerable<int> ids)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));

        foreach (var id in ids)
        {
            table.Rows.Add(id);
        }

        return table;
    }

    public async Task<ShopItemIdentifiers> AddAsync(PaintingCatalogAddition paintingCatalogAddition)
    {
        var images = CreateImageDataTable(paintingCatalogAddition);
        var paintingSeriesIds = CreateIdDataTable(
                paintingCatalogAddition.SeriesIds.Select((id) => id.Value)
            )
            .AsTableValuedParameter("dbo.IdList");
        var mediumIds = CreateIdDataTable(
                paintingCatalogAddition.MediumIds.Select((id) => id.Value)
            )
            .AsTableValuedParameter("dbo.IdList");
        var supportIds = CreateIdDataTable(
                paintingCatalogAddition.SupportIds.Select((id) => id.Value)
            )
            .AsTableValuedParameter("dbo.IdList");

        await using var connection = connectionFactory.Create();

        var row = await connection.QuerySingleAsync<AddedPaintingRow>(
            "dbo.AddPainting",
            new
            {
                Name = paintingCatalogAddition.Name.Value,
                CandidateSlug = paintingCatalogAddition.CandidateSlug.Value,
                paintingCatalogAddition.Price,
                paintingCatalogAddition.Stock,
                paintingCatalogAddition.DatePainted,
                paintingCatalogAddition.Description,
                WidthCm = paintingCatalogAddition.Dimensions?.Width,
                HeightCm = paintingCatalogAddition.Dimensions?.Height,
                Images = images.AsTableValuedParameter("dbo.ShopItemImageList"),
                SeriesIds = paintingSeriesIds,
                MediumIds = mediumIds,
                SupportIds = supportIds,
            },
            commandType: CommandType.StoredProcedure
        );

        return new ShopItemIdentifiers(new ShopItemId(row.Id), new ShopItemSlug(row.Slug));
    }

    public async Task<Painting?> GetBySlugAsync(string slug)
    {
        await using var connection = connectionFactory.Create();

        await using var results = await connection.QueryMultipleAsync(
            "dbo.GetPaintingBySlug",
            new { Slug = slug },
            commandType: CommandType.StoredProcedure
        );

        // These Read calls MUST run in the same order as the SELECTs in the procedure
        var row = await results.ReadSingleOrDefaultAsync<PaintingRow>();

        if (row is null)
        {
            return null;
        }

        var images = (await results.ReadAsync<ImageRow>()).ToList();

        var mediums = (await results.ReadAsync<LookupRow>())
            .Select(lookup => new Medium(lookup.Id, lookup.Name))
            .ToList();

        var supports = (await results.ReadAsync<LookupRow>())
            .Select(lookup => new Support(lookup.Id, lookup.Name))
            .ToList();

        var series = (await results.ReadAsync<LookupRow>())
            .Select(lookup => new PaintingSeries(lookup.Id, lookup.Name))
            .ToList();

        var dimensions = row is { WidthCm: decimal width, HeightCm: decimal height }
            ? new DimensionsCentimeters(new Dimensions(width, height))
            : null;

        var painting = new Painting(
            row.Id,
            new ShopItemName(row.Name),
            new ShopItemSlug(row.Slug),
            row.Price,
            row.Stock,
            row.DatePainted,
            images.Select(image => new ShopItemImage(
                image.RelativePath,
                image.Width,
                image.Height,
                image.BlurDataUri
            )),
            dimensions,
            row.Description,
            mediums,
            supports,
            series
        )
        {
            MainImageIndex = Math.Max(images.FindIndex(image => image.IsPrimary), 0),
        };

        return painting;
    }

    private sealed class AddedPaintingRow
    {
        public required int Id { get; init; }
        public required string Slug { get; init; }
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
        public required string RelativePath { get; init; }
        public required bool IsPrimary { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public string? BlurDataUri { get; init; }
    }

    private sealed class LookupRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
    }
}
