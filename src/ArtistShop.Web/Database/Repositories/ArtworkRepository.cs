namespace ArtistShop.Web.Database.Repositories;

using System.Data;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using Dapper;
using Npgsql;

public class ArtworkRepository(NpgsqlDataSource dataSource)
{
    // what our artwork procedures THROW when a choice changed while the form was open
    private static readonly string[] CatalogChangedErrors =
    [
        SqlStates.VocabularyTermNoLongerExists,
        SqlStates.SeriesNoLongerExists,
        SqlStates.ArtworkTypeNoLongerExists,
        SqlStates.ArtworkFieldSwitchedOff,
        SqlStates.ProductTypeNoLongerExists,
    ];

    private static DataTable CreateImageDataTable(
        IReadOnlyList<ArtworkImage> artworkImages,
        int mainImageIndex
    )
    {
        var images = new DataTable();
        images.Columns.Add("StorageKey", typeof(string));
        images.Columns.Add("OriginalFileName", typeof(string));
        images.Columns.Add("SortOrder", typeof(int));
        images.Columns.Add("IsPrimary", typeof(bool));
        images.Columns.Add("Width", typeof(int));
        images.Columns.Add("Height", typeof(int));
        images.Columns.Add("BlurDataUri", typeof(string));

        for (var i = 0; i < artworkImages.Count; i += 1)
        {
            var image = artworkImages[i];
            // must match the Table Value Property dbo.ArtworkImageList
            // parameter order
            images.Rows.Add(
                image.StorageKey,
                image.OriginalFileName,
                i,
                i == mainImageIndex,
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
        await using var connection = dataSource.CreateConnection();
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
        catch (PostgresException exception) when (IsCatalogChanged(exception))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    private static bool IsCatalogChanged(PostgresException exception) =>
        CatalogChangedErrors.Any(sqlState => SqlErrors.IsThrown(exception, sqlState));

    private static async Task<ArtworkIdentifiers> ExecuteAddAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ArtworkCatalogAddition artworkCatalogAddition,

        // the addition's own series plus the ones just created for it
        IReadOnlyList<SeriesId> resolvedSeriesIds
    )
    {
        var images = CreateImageDataTable(
            artworkCatalogAddition.Images,
            artworkCatalogAddition.MainImageIndex
        );
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

    // the artworks either side of this one in the series the visitor is walking through
    public async Task<ArtworkNeighbours> GetNeighboursInSeriesAsync(
        SeriesId seriesId,
        ArtworkId artworkId,
        bool onlyArtworksWithImages
    )
    {
        await using var connection = dataSource.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<ArtworkNeighboursRow>(
            "dbo.GetArtworkNeighboursInSeries",
            new
            {
                SeriesId = seriesId.Value,
                ArtworkId = artworkId.Value,
                OnlyArtworksWithImages = onlyArtworksWithImages,
            },
            commandType: CommandType.StoredProcedure
        );

        return new ArtworkNeighbours(
            ToLink(row?.PreviousName, row?.PreviousSlug),
            ToLink(row?.NextName, row?.NextSlug)
        );
    }

    private static ArtworkLink? ToLink(string? name, string? slug) =>
        name is not null && slug is not null
            ? new ArtworkLink(new ArtworkName(name), new ArtworkSlug(slug))
            : null;

    public async Task<List<string>> GetNamesOfTypeAsync(ArtworkTypeId artworkTypeId)
    {
        await using var connection = dataSource.CreateConnection();

        var names = await connection.QueryAsync<string>(
            "dbo.GetArtworkNames",
            new { ArtworkTypeId = artworkTypeId.Value },
            commandType: CommandType.StoredProcedure
        );
        return [.. names];
    }

    public Task<Artwork?> GetByIdAsync(ArtworkId id) =>
        GetAsync("dbo.GetArtworkById", new { Id = id.Value });

    public Task<Artwork?> GetBySlugAsync(string slug) =>
        GetAsync("dbo.GetArtworkBySlug", new { Slug = slug });

    // the search runs elsewhere and hands its matches in; null means nothing was searched for
    public async Task<ArtworkListPage> GetListAsync(
        ArtworkListFilter filter,
        IReadOnlyList<ArtworkId>? searchMatches
    )
    {
        await using var connection = dataSource.CreateConnection();

        var pageSize = CatalogLimits.ArtworkListPageSize;

        var rows = (
            await connection.QueryAsync<ArtworkListRow>(
                "dbo.GetArtworkList",
                new
                {
                    ArtworkTypeIds = IdListParameter.Create(
                        filter.ArtworkTypeIds.Select(id => id.Value)
                    ),
                    VocabularyTermIds = IdListParameter.Create(
                        filter.VocabularyTermIds.Select(id => id.Value)
                    ),
                    MatchingArtworkIds = IdListParameter.Create(
                        searchMatches?.Select(id => id.Value) ?? []
                    ),
                    IsSearching = searchMatches is not null,
                    SeriesId = filter.SeriesId?.Value,
                    filter.HasImages,
                    filter.IsForSale,
                    Sort = (byte)filter.Sort,
                    Offset = (filter.PageNumber - 1) * pageSize,
                    PageSize = pageSize,
                },
                commandType: CommandType.StoredProcedure
            )
        ).ToList();

        var items = rows.Select(row => new ArtworkListItem(
                new ArtworkId(row.Id),
                new ArtworkName(row.Name),
                new ArtworkSlug(row.Slug),
                new ArtworkTypeName(row.ArtworkTypeName),
                row is { DateCreated: DateOnly date, DateCreatedPrecision: DatePrecision precision }
                    ? new PartialDate(date, precision)
                    : null,
                row.SeriesNames,
                row.ImageCount,
                row.IsForSale,
                row.PrimaryImage
            ))
            .ToList();

        // the count rides on the rows, so an empty page carries none; the page component
        // sends a page number past the end back to the first page
        var totalCount = rows.Count is 0 ? 0 : rows[0].TotalCount;

        return new ArtworkListPage(items, totalCount, filter.PageNumber, pageSize);
    }

    // the slug comes back because the procedure decides it: a rename can land on a numbered one
    public async Task<ArtworkSlug> UpdateAsync(ArtworkCatalogUpdate artworkCatalogUpdate)
    {
        await using var connection = dataSource.CreateConnection();

        var images = CreateImageDataTable(
            artworkCatalogUpdate.Images,
            artworkCatalogUpdate.MainImageIndex
        );

        var vocabularyTermIds = IdListParameter.Create(
            artworkCatalogUpdate.VocabularyTermIds.Select(id => id.Value)
        );

        var seriesIds = IdListParameter.Create(
            artworkCatalogUpdate.SeriesIds.Select(id => id.Value)
        );

        try
        {
            var slug = await connection.QuerySingleAsync<string>(
                "dbo.UpdateArtwork",
                new
                {
                    Id = artworkCatalogUpdate.Id.Value,
                    Name = artworkCatalogUpdate.Name.Value,
                    CandidateSlug = artworkCatalogUpdate.CandidateSlug.Value,
                    artworkCatalogUpdate.Description,
                    DateCreated = artworkCatalogUpdate.DateCreated?.Date,
                    DateCreatedPrecision = artworkCatalogUpdate.DateCreated?.Precision,
                    HeightCm = artworkCatalogUpdate.Dimensions?.Height,
                    WidthCm = artworkCatalogUpdate.Dimensions?.Width,
                    DepthCm = artworkCatalogUpdate.Dimensions?.Depth,
                    DurationSeconds = (int?)artworkCatalogUpdate.Duration?.TotalSeconds,
                    Images = images.AsTableValuedParameter("dbo.ArtworkImageList"),
                    VocabularyTermIds = vocabularyTermIds,
                    SeriesIds = seriesIds,
                },
                commandType: CommandType.StoredProcedure
            );

            return new ArtworkSlug(slug);
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworkNoLongerExists))
        {
            throw new ArtworkDeletedException(exception.Message, exception);
        }
        catch (PostgresException exception) when (IsCatalogChanged(exception))
        {
            throw new CatalogChangedException(exception.Message, exception);
        }
    }

    // the images, series rows, term rows and products go with it, through the foreign keys'
    // ON DELETE CASCADE. The image files wait for OrphanedImageSweeper
    public async Task DeleteAsync(ArtworkId id)
    {
        await using var connection = dataSource.CreateConnection();

        await connection.ExecuteAsync(
            "dbo.DeleteArtwork",
            new { Id = id.Value },
            commandType: CommandType.StoredProcedure
        );
    }

    // both procedures return the same result sets, since GetArtworkBySlug runs GetArtworkById
    private async Task<Artwork?> GetAsync(string procedure, object parameters)
    {
        await using var connection = dataSource.CreateConnection();

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
                    new ProductTypeName(product.ProductTypeName),
                    product.ProductTypeIsDefault
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

    // every column is nullable: an artwork at either end of the series has no row to read there
    private sealed class ArtworkNeighboursRow
    {
        public string? PreviousName { get; init; }
        public string? PreviousSlug { get; init; }
        public string? NextName { get; init; }
        public string? NextSlug { get; init; }
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
        public required bool ProductTypeIsDefault { get; init; }
        public string? Label { get; init; }
        public decimal? Price { get; init; }
        public int? EditionSize { get; init; }
        public required int Stock { get; init; }
    }

    private sealed class ArtworkListRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public required string ArtworkTypeName { get; init; }
        public required DateOnly? DateCreated { get; init; }
        public required DatePrecision? DateCreatedPrecision { get; init; }
        public required int ImageCount { get; init; }
        public required bool IsForSale { get; init; }
        public string? SeriesNames { get; init; }
        public string? PrimaryImageStorageKey { get; init; }
        public int? PrimaryImageWidth { get; init; }
        public int? PrimaryImageHeight { get; init; }
        public string? PrimaryImageBlurDataUri { get; init; }
        public required int TotalCount { get; init; }

        // an artwork with no image leaves all four image columns null, and matching all three
        // of the non-null ones together is what lets the width and height be read as plain ints
        public ArtworkImage? PrimaryImage =>
            this is
            {
                PrimaryImageStorageKey: string storageKey,
                PrimaryImageWidth: int width,
                PrimaryImageHeight: int height,
            }
                ? new ArtworkImage(storageKey, null, width, height, PrimaryImageBlurDataUri)
                : null;
    }
}
