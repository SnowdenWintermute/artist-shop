namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using Dapper;
using Microsoft.Data.SqlClient;

public class ArtworkRepository(SqlConnectionFactory connectionFactory)
{
    // what AddArtwork THROWs when a choice changed while the form was open
    private static readonly int[] CatalogChangedErrors =
    [
        SqlErrorNumbers.VocabularyTermNoLongerExists,
        SqlErrorNumbers.SeriesNoLongerExists,
        SqlErrorNumbers.ArtworkTypeNoLongerExists,
        SqlErrorNumbers.ArtworkFieldSwitchedOff,
        SqlErrorNumbers.ProductTypeNoLongerExists,
    ];

    private static DataTable CreateImageDataTable(ArtworkCatalogAddition artworkCatalogAddition)
    {
        var images = new DataTable();
        images.Columns.Add("StorageKey", typeof(string));
        images.Columns.Add("OriginalFileName", typeof(string));
        images.Columns.Add("SortOrder", typeof(int));
        images.Columns.Add("IsPrimary", typeof(bool));
        images.Columns.Add("Width", typeof(int));
        images.Columns.Add("Height", typeof(int));
        images.Columns.Add("BlurDataUri", typeof(string));

        for (var i = 0; i < artworkCatalogAddition.Images.Count; i += 1)
        {
            var image = artworkCatalogAddition.Images[i];
            // must match the Table Value Property dbo.ArtworkImageList
            // parameter order
            images.Rows.Add(
                image.StorageKey,
                image.OriginalFileName,
                i,
                i == artworkCatalogAddition.MainImageIndex,
                image.Width,
                image.Height,
                image.BlurDataUri
            );
        }

        return images;
    }

    private static DataTable CreateProductDataTable(IReadOnlyList<ProductAddition> products)
    {
        var table = new DataTable();
        // must match dbo.ProductList, in the same order
        table.Columns.Add("ProductTypeId", typeof(int));
        table.Columns.Add("Label", typeof(string));
        table.Columns.Add("Price", typeof(decimal));
        table.Columns.Add("EditionSize", typeof(int));
        table.Columns.Add("Stock", typeof(int));

        foreach (var product in products)
        {
            // a DataTable stores a missing value as DBNull, not null
            table.Rows.Add(
                product.TypeId.Value,
                (object?)product.Label ?? DBNull.Value,
                (object?)product.Price ?? DBNull.Value,
                (object?)product.EditionSize ?? DBNull.Value,
                product.Stock
            );
        }

        return table;
    }

    // one addition takes the same path as many, so it can name a series that doesn't exist yet
    public async Task<ArtworkIdentifiers> AddAsync(ArtworkCatalogAddition artworkCatalogAddition) =>
        (await AddManyAsync([artworkCatalogAddition]))[0];

    // all or nothing: a failure on any artwork rolls back the ones before it
    public async Task<List<ArtworkIdentifiers>> AddManyAsync(
        IReadOnlyList<ArtworkCatalogAddition> artworkCatalogAdditions
    )
    {
        await using var connection = connectionFactory.Create();
        // Dapper opens a closed connection by itself, but a transaction needs it open first
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            var identifiers = new List<ArtworkIdentifiers>();
            var newSeriesIds = await SeriesRepository.AddManyAsync(
                connection,
                transaction,
                [.. artworkCatalogAdditions.SelectMany(artworkCatalogAddition => artworkCatalogAddition.NewSeriesNames)]
            );

            // AddArtwork's own BEGIN and COMMIT nest inside this transaction, so its COMMIT only
            // counts down; nothing is saved until the CommitAsync below
            foreach (var artworkCatalogAddition in artworkCatalogAdditions)
            {
                var seriesIds = artworkCatalogAddition
                    .SeriesIds.Concat(artworkCatalogAddition.NewSeriesNames.Select(name => newSeriesIds[name.Value]))
                    .ToList();

                identifiers.Add(await ExecuteAddAsync(connection, transaction, artworkCatalogAddition, seriesIds));
            }

            await transaction.CommitAsync();
            return identifiers;
        }
        catch (SqlException exception) when (IsCatalogChanged(exception))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    private static bool IsCatalogChanged(SqlException exception) =>
        CatalogChangedErrors.Any(number => SqlErrors.IsThrown(exception, number));


    private static async Task<ArtworkIdentifiers> ExecuteAddAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        ArtworkCatalogAddition artworkCatalogAddition,
        // the addition's own series plus the ones just created for it
        IReadOnlyList<SeriesId> resolvedSeriesIds
    )
    {
        var images = CreateImageDataTable(artworkCatalogAddition);
        var products = CreateProductDataTable(artworkCatalogAddition.Products);
        var seriesIds = IdListParameter.Create(resolvedSeriesIds.Select(id => id.Value));

        var vocabularyTermIds = IdListParameter.Create(
            artworkCatalogAddition.VocabularyTermIds.Select(id => id.Value)
        );

        var row = await connection.QuerySingleAsync<AddedArtworkRow>(
            "dbo.AddArtwork",
            new
            {
                ArtworkTypeId = artworkCatalogAddition.TypeId.Value,
                Name = artworkCatalogAddition.Name.Value,
                CandidateSlug = artworkCatalogAddition.CandidateSlug.Value,
                artworkCatalogAddition.Description,
                DateCreated = artworkCatalogAddition.DateCreated?.Date,
                DateCreatedPrecision = artworkCatalogAddition.DateCreated?.Precision,
                HeightCm = artworkCatalogAddition.Dimensions?.Height,
                WidthCm = artworkCatalogAddition.Dimensions?.Width,
                DepthCm = artworkCatalogAddition.Dimensions?.Depth,
                DurationSeconds = (int?)artworkCatalogAddition.Duration?.TotalSeconds,
                Images = images.AsTableValuedParameter("dbo.ArtworkImageList"),
                SeriesIds = seriesIds,
                VocabularyTermIds = vocabularyTermIds,
                Products = products.AsTableValuedParameter("dbo.ProductList"),
            },
            transaction,
            commandType: CommandType.StoredProcedure
        );

        return new ArtworkIdentifiers(new ArtworkId(row.Id), new ArtworkSlug(row.Slug));
    }

    public async Task<List<string>> GetAllNamesAsync()
    {
        await using var connection = connectionFactory.Create();

        var names = await connection.QueryAsync<string>(
            "dbo.GetArtworkNames",
            commandType: CommandType.StoredProcedure
        );
        return [.. names];
    }

    public Task<Artwork?> GetByIdAsync(ArtworkId id) =>
        GetAsync("dbo.GetArtworkById", new { Id = id.Value });

    public Task<Artwork?> GetBySlugAsync(string slug) =>
        GetAsync("dbo.GetArtworkBySlug", new { Slug = slug });

    // both procedures return the same result sets, since GetArtworkBySlug runs GetArtworkById
    private async Task<Artwork?> GetAsync(string procedure, object parameters)
    {
        await using var connection = connectionFactory.Create();

        await using var results = await connection.QueryMultipleAsync(
            procedure,
            parameters,
            commandType: CommandType.StoredProcedure
        );

        // These Read calls MUST run in the same order as the SELECTs in the procedure
        var row = await results.ReadSingleOrDefaultAsync<ArtworkRow>();

        if (row is null)
        {
            return null;
        }

        var images = (await results.ReadAsync<ImageRow>()).ToList();

        var series = (await results.ReadAsync<SeriesRow>())
            .Select(seriesRow => new Series(
                new SeriesId(seriesRow.Id),
                new SeriesName(seriesRow.Name),
                new SeriesSlug(seriesRow.Slug)
            ))
            .ToList();

        var vocabularyTerms = (await results.ReadAsync<VocabularyTermRow>())
            .Select(term => new VocabularyTerm(
                new VocabularyTermId(term.Id),
                new VocabularyTermName(term.Name),
                new VocabularyId(term.VocabularyId),
                new VocabularyName(term.VocabularyName)
            ))
            .ToList();

        var products = (await results.ReadAsync<ProductRow>())
            .Select(product => new Product(
                new ProductId(product.Id),
                new ProductType(
                    new ProductTypeId(product.ProductTypeId),
                    new ProductTypeName(product.ProductTypeName)
                ),
                product.Label,
                product.Price,
                product.EditionSize,
                product.Stock
            ))
            .ToList();

        var dimensions = row is { HeightCm: decimal height, WidthCm: decimal width }
            ? new DimensionsCentimeters(new Dimensions(height, width, row.DepthCm))
            : null;

        var createdDate = row.DateCreated;
        var precision = row.DateCreatedPrecision;

        var dateCreated =
            createdDate is not null && precision is not null
                ? new PartialDate(createdDate.Value, precision.Value)
                : null;

        var duration = row.DurationSeconds is int seconds ? TimeSpan.FromSeconds(seconds) : (TimeSpan?)null;

        var artwork = new Artwork(
            new ArtworkId(row.Id),
            new ArtworkType(new ArtworkTypeId(row.ArtworkTypeId), new ArtworkTypeName(row.ArtworkTypeName)),
            new ArtworkName(row.Name),
            new ArtworkSlug(row.Slug),
            row.Description,
            dateCreated,
            dimensions,
            duration,
            images.Select(image => new ArtworkImage(
                image.StorageKey,
                image.OriginalFileName,
                image.Width,
                image.Height,
                image.BlurDataUri
            )),
            series,
            vocabularyTerms,
            products
        )
        {
            MainImageIndex = Math.Max(images.FindIndex(image => image.IsPrimary), 0),
        };

        return artwork;
    }

    private sealed class AddedArtworkRow
    {
        public required int Id { get; init; }
        public required string Slug { get; init; }
    }


    private sealed class ArtworkRow
    {
        public required int Id { get; init; }
        public required int ArtworkTypeId { get; init; }
        public required string ArtworkTypeName { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public string? Description { get; init; }
        public required DateOnly? DateCreated { get; init; }
        public required DatePrecision? DateCreatedPrecision { get; init; }
        public decimal? HeightCm { get; init; }
        public decimal? WidthCm { get; init; }
        public decimal? DepthCm { get; init; }
        public int? DurationSeconds { get; init; }
    }

    private sealed class ImageRow
    {
        public required string StorageKey { get; init; }
        public string? OriginalFileName { get; init; }
        public required bool IsPrimary { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public string? BlurDataUri { get; init; }
    }

    private sealed class SeriesRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
    }

    private sealed class VocabularyTermRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required int VocabularyId { get; init; }
        public required string VocabularyName { get; init; }
    }

    private sealed class ProductRow
    {
        public required int Id { get; init; }
        public required int ProductTypeId { get; init; }
        public required string ProductTypeName { get; init; }
        public string? Label { get; init; }
        public decimal? Price { get; init; }
        public int? EditionSize { get; init; }
        public required int Stock { get; init; }
    }
}
