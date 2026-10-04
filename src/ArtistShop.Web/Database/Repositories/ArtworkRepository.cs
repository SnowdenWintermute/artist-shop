namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using Dapper;
using Npgsql;

public class ArtworkRepository(SiteDatabase database)
{
    // what our artwork functions RAISE when a choice changed while the form was open
    private static readonly string[] CatalogChangedErrors =
    [
        SqlStates.VocabularyTermNoLongerExists,
        SqlStates.SeriesNoLongerExists,
        SqlStates.ArtworkTypeNoLongerExists,
        SqlStates.ArtworkFieldSwitchedOff,
        SqlStates.ProductTypeNoLongerExists,
    ];

    private static ArtworkImageInput[] CreateImageInputs(
        IReadOnlyList<ArtworkImage> artworkImages,
        int mainImageIndex
    ) =>
        [
            .. artworkImages.Select(
                (image, index) =>
                    new ArtworkImageInput(
                        image.StorageKey,
                        image.OriginalFileName,
                        index,
                        index == mainImageIndex,
                        image.Width,
                        image.Height,
                        image.BlurDataUri
                    )
            ),
        ];

    private static ProductInput[] CreateProductInputs(IReadOnlyList<ProductAddition> products) =>
        [
            .. products.Select(product => new ProductInput(
                product.TypeId.Value,
                product.Label,
                product.Price,
                product.EditionSize,
                product.Stock
            )),
        ];

    // one addition takes the same path as many, so it can name a series that doesn't exist yet
    public async Task<ArtworkIdentifiers> AddAsync(ArtworkCatalogAddition artworkCatalogAddition) =>
        (await AddManyAsync([artworkCatalogAddition]))[0];

    // all or nothing: a failure on any artwork rolls back the ones before it
    public async Task<List<ArtworkIdentifiers>> AddManyAsync(
        IReadOnlyList<ArtworkCatalogAddition> artworkCatalogAdditions
    )
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            var identifiers = new List<ArtworkIdentifiers>();
            var newSeriesIds = await SeriesRepository.AddManyAsync(
                connection,
                transaction,
                [
                    .. artworkCatalogAdditions.SelectMany(artworkCatalogAddition =>
                        artworkCatalogAddition.NewSeriesNames
                    ),
                ]
            );

            // add_artwork runs inside this transaction, so nothing is saved until the CommitAsync
            // below
            foreach (var artworkCatalogAddition in artworkCatalogAdditions)
            {
                var seriesIds = artworkCatalogAddition
                    .SeriesIds.Concat(
                        artworkCatalogAddition.NewSeriesNames.Select(name =>
                            newSeriesIds[name.Value]
                        )
                    )
                    .ToList();

                identifiers.Add(
                    await ExecuteAddAsync(
                        connection,
                        transaction,
                        artworkCatalogAddition,
                        seriesIds
                    )
                );
            }

            await transaction.CommitAsync();
            return identifiers;
        }
        catch (PostgresException exception) when (IsCatalogChanged(exception))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
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
        var row = await connection.QuerySingleAsync<AddedArtworkRow>(
            """
            SELECT * FROM add_artwork(
                @ArtworkTypeId, @Name, @CandidateSlug, @Description, @DateCreated, @DateCreatedPrecision,
                @HeightCm, @WidthCm, @DepthCm, @DurationSeconds,
                @Images, @VocabularyTermIds, @SeriesIds, @Products
            )
            """,
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
                Images = CreateImageInputs(
                    artworkCatalogAddition.Images,
                    artworkCatalogAddition.MainImageIndex
                ),
                VocabularyTermIds = (int[])
                    [.. artworkCatalogAddition.VocabularyTermIds.Select(id => id.Value)],
                SeriesIds = (int[])[.. resolvedSeriesIds.Select(id => id.Value)],
                Products = CreateProductInputs(artworkCatalogAddition.Products),
            },
            transaction
        );

        return new ArtworkIdentifiers(new ArtworkId(row.Id), new ArtworkSlug(row.Slug));
    }

    // the artworks either side of this one in the series the visitor is walking through, running on
    // into the series before or after at either end
    public async Task<ArtworkNeighbours> GetNeighboursAsync(
        SeriesId seriesId,
        ArtworkId artworkId,
        bool onlyArtworksWithImages
    )
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleOrDefaultAsync<ArtworkNeighboursRow>(
            "SELECT * FROM get_artwork_neighbours(@SeriesId, @ArtworkId, @OnlyArtworksWithImages)",
            new
            {
                SeriesId = seriesId.Value,
                ArtworkId = artworkId.Value,
                OnlyArtworksWithImages = onlyArtworksWithImages,
            }
        );

        return new ArtworkNeighbours(
            ToArtworkInSeries(row?.PreviousArtworkSlug, row?.PreviousSeriesSlug, row?.PreviousImageCount),
            ToArtworkInSeries(row?.NextArtworkSlug, row?.NextSeriesSlug, row?.NextImageCount)
        );
    }

    // the artwork must be in the series, as it is for any series taken from its own list
    public async Task<ArtworkWalkPosition> GetWalkPositionAsync(SeriesId seriesId, ArtworkId artworkId)
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleAsync<ArtworkWalkPositionRow>(
            "SELECT * FROM get_artwork_walk_position(@SeriesId, @ArtworkId)",
            new { SeriesId = seriesId.Value, ArtworkId = artworkId.Value }
        );

        return new ArtworkWalkPosition(row.EarlierImageCount, row.TotalImageCount);
    }

    private static ArtworkInSeries? ToArtworkInSeries(string? slug, string? seriesSlug, int? imageCount) =>
        slug is not null && seriesSlug is not null && imageCount is int count
            ? new ArtworkInSeries(new ArtworkSlug(slug), new SeriesSlug(seriesSlug), count)
            : null;

    public async Task<List<ArtworkTitleAndSlug>> GetTitlesOfTypeAsync(ArtworkTypeId artworkTypeId)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<TitleAndSlugRow>(
            "SELECT * FROM get_artwork_names(@ArtworkTypeId)",
            new { ArtworkTypeId = artworkTypeId.Value }
        );
        return [.. rows.Select(row => new ArtworkTitleAndSlug(row.Name, row.Slug))];
    }

    private sealed class TitleAndSlugRow
    {
        public required string Name { get; init; }
        public required string Slug { get; init; }
    }

    public Task<Artwork?> GetByIdAsync(ArtworkId id) => GetAsync(id.Value);

    // by storage key. A deleted image has no entry
    public async Task<Dictionary<string, ArtworkImageWithArtwork>> GetImagesByStorageKeyAsync(
        IReadOnlyCollection<string> storageKeys
    )
    {
        if (storageKeys.Count == 0)
        {
            return [];
        }

        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ImageWithArtworkRow>(
            "SELECT * FROM get_artwork_images_by_storage_keys(@StorageKeys)",
            new { StorageKeys = storageKeys.ToArray() }
        );

        return rows.ToDictionary(
            row => row.StorageKey,
            row => new ArtworkImageWithArtwork(
                new ArtworkId(row.ArtworkId),
                new ArtworkLink(new ArtworkName(row.ArtworkName), new ArtworkSlug(row.ArtworkSlug)),
                new ArtworkImage(row.StorageKey, row.OriginalFileName, row.Width, row.Height, row.BlurDataUri),
                row.ImageNumber
            )
        );
    }

    public async Task<Artwork?> GetBySlugAsync(string slug)
    {
        int? id;

        await using (var connection = await database.OpenConnectionAsync())
        {
            id = await connection.QuerySingleAsync<int?>(
                "SELECT get_artwork_id_by_slug(@Slug)",
                new { Slug = slug }
            );
        }

        return id is int artworkId ? await GetAsync(artworkId) : null;
    }

    // the search runs elsewhere and hands its matches in; null means nothing was searched for
    // excludedSeriesId leaves out the artworks already in that series, for a page adding to it
    public async Task<ArtworkListPage> GetListAsync(
        ArtworkListFilter filter,
        IReadOnlyList<ArtworkId>? searchMatches,
        SeriesId? excludedSeriesId
    )
    {
        await using var connection = await database.OpenConnectionAsync();

        var pageSize = ArtistShopLimits.ArtworkListPageSize;

        int? seriesId = filter.Series is ArtworkSeriesFilter.InSeries inSeries ? inSeries.SeriesId.Value : null;

        var rows = (
            await connection.QueryAsync<ArtworkListRow>(
                """
                SELECT * FROM get_artwork_list(
                    @ArtworkTypeIds, @VocabularyTermIds, @MatchingArtworkIds, @IsSearching,
                    @SeriesId, @IsInNoSeries, @ExcludedSeriesId, @HasImages, @IsForSale, @Sort, @Offset, @PageSize
                )
                """,
                new
                {
                    ArtworkTypeIds = (int[])[.. filter.ArtworkTypeIds.Select(id => id.Value)],
                    VocabularyTermIds = (int[])[.. filter.VocabularyTermIds.Select(id => id.Value)],
                    MatchingArtworkIds = (int[])[.. searchMatches?.Select(id => id.Value) ?? []],
                    IsSearching = searchMatches is not null,
                    SeriesId = seriesId,
                    IsInNoSeries = filter.Series is ArtworkSeriesFilter.InNoSeries,
                    ExcludedSeriesId = excludedSeriesId?.Value,
                    filter.HasImages,
                    filter.IsForSale,
                    Sort = (short)filter.Sort,
                    Offset = (filter.PageNumber - 1) * pageSize,
                    PageSize = pageSize,
                }
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

    // the slug comes back because the function decides it: a rename can land on a numbered one
    public async Task<ArtworkSlug> UpdateAsync(ArtworkCatalogUpdate artworkCatalogUpdate)
    {
        var parameters = DetailsParameters(artworkCatalogUpdate.Details);
        parameters.Add(
            "Images",
            CreateImageInputs(artworkCatalogUpdate.Images, artworkCatalogUpdate.MainImageIndex)
        );

        return await ExecuteUpdateAsync(
            """
            SELECT update_artwork(
                @Id, @Name, @CandidateSlug, @Description, @DateCreated, @DateCreatedPrecision,
                @HeightCm, @WidthCm, @DepthCm, @DurationSeconds,
                @Images, @VocabularyTermIds, @SeriesIds
            )
            """,
            parameters
        );
    }

    // leaves the images alone, so uploads appending to the artwork meanwhile keep theirs
    public Task<ArtworkSlug> UpdateDetailsAsync(ArtworkDetailsUpdate details) =>
        ExecuteUpdateAsync(
            """
            SELECT update_artwork_details(
                @Id, @Name, @CandidateSlug, @Description, @DateCreated, @DateCreatedPrecision,
                @HeightCm, @WidthCm, @DepthCm, @DurationSeconds,
                @VocabularyTermIds, @SeriesIds
            )
            """,
            DetailsParameters(details)
        );

    private static DynamicParameters DetailsParameters(ArtworkDetailsUpdate details) =>
        new(
            new
            {
                Id = details.Id.Value,
                Name = details.Name.Value,
                CandidateSlug = details.CandidateSlug.Value,
                details.Description,
                DateCreated = details.DateCreated?.Date,
                DateCreatedPrecision = details.DateCreated?.Precision,
                HeightCm = details.Dimensions?.Height,
                WidthCm = details.Dimensions?.Width,
                DepthCm = details.Dimensions?.Depth,
                DurationSeconds = (int?)details.Duration?.TotalSeconds,
                VocabularyTermIds = (int[])[.. details.VocabularyTermIds.Select(id => id.Value)],
                SeriesIds = (int[])[.. details.SeriesIds.Select(id => id.Value)],
            }
        );

    private async Task<ArtworkSlug> ExecuteUpdateAsync(string sql, DynamicParameters parameters)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            return new ArtworkSlug(await connection.QuerySingleAsync<string>(sql, parameters));
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.ArtworkNoLongerExists))
        {
            throw new ArtworkDeletedException(exception.Message, exception);
        }
        catch (PostgresException exception) when (IsCatalogChanged(exception))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    // the images, series rows, term rows and products go with it, through the foreign keys'
    // ON DELETE CASCADE. The image files wait for OrphanedImageSweeper
    public async Task DeleteAsync(ArtworkId id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync("SELECT delete_artwork(@Id)", new { Id = id.Value });
    }

    private async Task<Artwork?> GetAsync(int id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_artwork(@Id);
            SELECT * FROM get_artwork_images(@Id);
            SELECT * FROM get_artwork_series(@Id);
            SELECT * FROM get_artwork_vocabulary_terms(@Id);
            SELECT * FROM get_artwork_products(@Id);
            """,
            new { Id = id }
        );

        // These Read calls MUST run in the same order as the SELECTs above
        var row = await results.ReadSingleOrDefaultAsync<ArtworkRow>();

        if (row is null)
        {
            return null;
        }

        return ToArtwork(
            row,
            [.. await results.ReadAsync<ImageRow>()],
            [.. await results.ReadAsync<SeriesRow>()],
            [.. await results.ReadAsync<VocabularyTermRow>()],
            [.. await results.ReadAsync<ProductRow>()]
        );
    }

    // every artwork on the site with its images, series, terms and products, for the site export
    public async Task<List<Artwork>> GetAllAsync()
    {
        await using var connection = await database.OpenConnectionAsync();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_all_artworks();
            SELECT * FROM get_all_artwork_images();
            SELECT * FROM get_all_artwork_series();
            SELECT * FROM get_all_artwork_vocabulary_terms();
            SELECT * FROM get_all_artwork_products();
            """
        );

        // These Read calls MUST run in the same order as the SELECTs above
        var rows = await results.ReadAsync<ArtworkRow>();
        var images = (await results.ReadAsync<ImageRowWithArtworkId>()).ToLookup(image => image.ArtworkId);
        var series = (await results.ReadAsync<SeriesRowWithArtworkId>()).ToLookup(series => series.ArtworkId);
        var terms = (await results.ReadAsync<VocabularyTermRowWithArtworkId>()).ToLookup(term => term.ArtworkId);
        var products = (await results.ReadAsync<ProductRowWithArtworkId>()).ToLookup(product => product.ArtworkId);

        return
        [
            .. rows.Select(row =>
                ToArtwork(row, [.. images[row.Id]], [.. series[row.Id]], [.. terms[row.Id]], [.. products[row.Id]])
            ),
        ];
    }

    private static Artwork ToArtwork(
        ArtworkRow row,
        List<ImageRow> images,
        List<SeriesRow> seriesRows,
        List<VocabularyTermRow> termRows,
        List<ProductRow> productRows
    )
    {
        var series = seriesRows
            .Select(seriesRow => new Series(
                new SeriesId(seriesRow.Id),
                new SeriesName(seriesRow.Name),
                new SeriesSlug(seriesRow.Slug)
            ))
            .ToList();

        var vocabularyTerms = termRows
            .Select(term => new VocabularyTerm(
                new VocabularyTermId(term.Id),
                new VocabularyTermName(term.Name),
                new VocabularyId(term.VocabularyId),
                new VocabularyName(term.VocabularyName)
            ))
            .ToList();

        var products = productRows
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

        var duration = row.DurationSeconds is int seconds
            ? TimeSpan.FromSeconds(seconds)
            : (TimeSpan?)null;

        var artwork = new Artwork(
            new ArtworkId(row.Id),
            new ArtworkType(
                new ArtworkTypeId(row.ArtworkTypeId),
                new ArtworkTypeName(row.ArtworkTypeName)
            ),
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

    // every column is nullable: an artwork at the very first or last place has no row to read there
    private sealed class ArtworkNeighboursRow
    {
        public string? PreviousSeriesSlug { get; init; }
        public string? PreviousArtworkSlug { get; init; }
        public int? PreviousImageCount { get; init; }
        public string? NextSeriesSlug { get; init; }
        public string? NextArtworkSlug { get; init; }
        public int? NextImageCount { get; init; }
    }

    private sealed class ArtworkWalkPositionRow
    {
        public required int EarlierImageCount { get; init; }
        public required int TotalImageCount { get; init; }
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

    private class ImageRow
    {
        public required string StorageKey { get; init; }
        public string? OriginalFileName { get; init; }
        public required bool IsPrimary { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public string? BlurDataUri { get; init; }
    }

    private sealed class ImageWithArtworkRow
    {
        public required int ArtworkId { get; init; }
        public required string ArtworkName { get; init; }
        public required string ArtworkSlug { get; init; }
        public required string StorageKey { get; init; }
        public string? OriginalFileName { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public string? BlurDataUri { get; init; }
        public required int ImageNumber { get; init; }
    }

    private class SeriesRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
    }

    private class VocabularyTermRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required int VocabularyId { get; init; }
        public required string VocabularyName { get; init; }
    }

    private class ProductRow
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

    // get_all_artwork_*'s rows, which say whose they are
    private sealed class ImageRowWithArtworkId : ImageRow
    {
        public required int ArtworkId { get; init; }
    }

    private sealed class SeriesRowWithArtworkId : SeriesRow
    {
        public required int ArtworkId { get; init; }
    }

    private sealed class VocabularyTermRowWithArtworkId : VocabularyTermRow
    {
        public required int ArtworkId { get; init; }
    }

    private sealed class ProductRowWithArtworkId : ProductRow
    {
        public required int ArtworkId { get; init; }
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
            this
                is {
                    PrimaryImageStorageKey: string storageKey,
                    PrimaryImageWidth: int width,
                    PrimaryImageHeight: int height,
                }
                ? new ArtworkImage(storageKey, null, width, height, PrimaryImageBlurDataUri)
                : null;
    }
}
