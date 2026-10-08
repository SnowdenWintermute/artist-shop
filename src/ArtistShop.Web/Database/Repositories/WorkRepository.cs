namespace ArtistShop.Web.Database.Repositories;

using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using Dapper;
using Npgsql;

public class WorkRepository(SiteDatabase database)
{
    // what our work functions RAISE when a choice changed while the form was open
    private static readonly string[] CatalogChangedErrors =
    [
        SqlStates.VocabularyTermNoLongerExists,
        SqlStates.CollectionNoLongerExists,
        SqlStates.WorkTypeNoLongerExists,
        SqlStates.WorkFieldSwitchedOff,
        SqlStates.ProductTypeNoLongerExists,
        SqlStates.VocabularyBecameMutuallyExclusive,
    ];

    private static WorkImageInput[] CreateImageInputs(
        IReadOnlyList<WorkImage> workImages,
        int mainImageIndex
    ) =>
        [
            .. workImages.Select(
                (image, index) =>
                    new WorkImageInput(
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

    // one addition takes the same path as many, so it can name a collection that doesn't exist yet
    public async Task<WorkIdentifiers> AddAsync(WorkCatalogAddition workCatalogAddition) =>
        (await AddManyAsync([workCatalogAddition]))[0];

    // all or nothing: a failure on any work rolls back the ones before it
    public async Task<List<WorkIdentifiers>> AddManyAsync(
        IReadOnlyList<WorkCatalogAddition> workCatalogAdditions
    )
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            var identifiers = new List<WorkIdentifiers>();
            var newCollectionIds = await CollectionRepository.AddManyAsync(
                connection,
                transaction,
                [
                    .. workCatalogAdditions.SelectMany(workCatalogAddition =>
                        workCatalogAddition.NewCollectionNames
                    ),
                ]
            );

            // add_work runs inside this transaction, so nothing is saved until the CommitAsync
            // below
            foreach (var workCatalogAddition in workCatalogAdditions)
            {
                var collectionIds = workCatalogAddition
                    .CollectionIds.Concat(
                        workCatalogAddition.NewCollectionNames.Select(name =>
                            newCollectionIds[name.Value]
                        )
                    )
                    .ToList();

                identifiers.Add(
                    await ExecuteAddAsync(
                        connection,
                        transaction,
                        workCatalogAddition,
                        collectionIds
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

    private static async Task<WorkIdentifiers> ExecuteAddAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkCatalogAddition workCatalogAddition,
        // the addition's own collections plus the ones just created for it
        IReadOnlyList<CollectionId> resolvedCollectionIds
    )
    {
        var row = await connection.QuerySingleAsync<AddedWorkRow>(
            """
            SELECT * FROM add_work(
                @WorkTypeId, @Name, @CandidateSlug, @Description, @DateCreated, @DateCreatedPrecision,
                @HeightCm, @WidthCm, @DepthCm, @DurationSeconds,
                @Images, @VocabularyTermIds, @CollectionIds, @Products
            )
            """,
            new
            {
                WorkTypeId = workCatalogAddition.TypeId.Value,
                Name = workCatalogAddition.Name.Value,
                CandidateSlug = workCatalogAddition.CandidateSlug.Value,
                workCatalogAddition.Description,
                DateCreated = workCatalogAddition.DateCreated?.Date,
                DateCreatedPrecision = workCatalogAddition.DateCreated?.Precision,
                HeightCm = workCatalogAddition.Dimensions?.Height,
                WidthCm = workCatalogAddition.Dimensions?.Width,
                DepthCm = workCatalogAddition.Dimensions?.Depth,
                DurationSeconds = (int?)workCatalogAddition.Duration?.TotalSeconds,
                Images = CreateImageInputs(
                    workCatalogAddition.Images,
                    workCatalogAddition.MainImageIndex
                ),
                VocabularyTermIds = (int[])
                    [.. workCatalogAddition.VocabularyTermIds.Select(id => id.Value)],
                CollectionIds = (int[])[.. resolvedCollectionIds.Select(id => id.Value)],
                Products = CreateProductInputs(workCatalogAddition.Products),
            },
            transaction
        );

        return new WorkIdentifiers(new WorkId(row.Id), new WorkSlug(row.Slug));
    }

    // the works either side of this one in the collection the visitor is walking through, running on
    // into the collection before or after at either end
    public async Task<WorkNeighbours> GetNeighboursAsync(
        CollectionId collectionId,
        WorkId workId,
        bool onlyWorksWithImages
    )
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleOrDefaultAsync<WorkNeighboursRow>(
            "SELECT * FROM get_work_neighbours(@CollectionId, @WorkId, @OnlyWorksWithImages)",
            new
            {
                CollectionId = collectionId.Value,
                WorkId = workId.Value,
                OnlyWorksWithImages = onlyWorksWithImages,
            }
        );

        return new WorkNeighbours(
            ToWorkInCollection(row?.PreviousWorkSlug, row?.PreviousCollectionSlug, row?.PreviousImageCount),
            ToWorkInCollection(row?.NextWorkSlug, row?.NextCollectionSlug, row?.NextImageCount)
        );
    }

    // the work must be in the collection, as it is for any collection taken from its own list
    public async Task<WorkWalkPosition> GetWalkPositionAsync(CollectionId collectionId, WorkId workId)
    {
        await using var connection = await database.OpenConnectionAsync();

        var row = await connection.QuerySingleAsync<WorkWalkPositionRow>(
            "SELECT * FROM get_work_walk_position(@CollectionId, @WorkId)",
            new { CollectionId = collectionId.Value, WorkId = workId.Value }
        );

        return new WorkWalkPosition(row.EarlierImageCount, row.TotalImageCount);
    }

    private static WorkInCollection? ToWorkInCollection(string? slug, string? collectionSlug, int? imageCount) =>
        slug is not null && collectionSlug is not null && imageCount is int count
            ? new WorkInCollection(new WorkSlug(slug), new CollectionSlug(collectionSlug), count)
            : null;

    public async Task<List<WorkTitleAndSlug>> GetTitlesOfTypeAsync(WorkTypeId workTypeId)
    {
        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<TitleAndSlugRow>(
            "SELECT * FROM get_work_names(@WorkTypeId)",
            new { WorkTypeId = workTypeId.Value }
        );
        return [.. rows.Select(row => new WorkTitleAndSlug(row.Name, row.Slug))];
    }

    private sealed class TitleAndSlugRow
    {
        public required string Name { get; init; }
        public required string Slug { get; init; }
    }

    public Task<Work?> GetByIdAsync(WorkId id) => GetAsync(id.Value);

    // by storage key. A deleted image has no entry
    public async Task<Dictionary<string, WorkImageWithWork>> GetImagesByStorageKeyAsync(
        IReadOnlyCollection<string> storageKeys
    )
    {
        if (storageKeys.Count == 0)
        {
            return [];
        }

        await using var connection = await database.OpenConnectionAsync();

        var rows = await connection.QueryAsync<ImageWithWorkRow>(
            "SELECT * FROM get_work_images_by_storage_keys(@StorageKeys)",
            new { StorageKeys = storageKeys.ToArray() }
        );

        return rows.ToDictionary(
            row => row.StorageKey,
            row => new WorkImageWithWork(
                new WorkId(row.WorkId),
                new WorkLink(new WorkName(row.WorkName), new WorkSlug(row.WorkSlug)),
                new WorkImage(row.StorageKey, row.OriginalFileName, row.Width, row.Height, row.BlurDataUri),
                row.ImageNumber
            )
        );
    }

    public async Task<Work?> GetBySlugAsync(string slug)
    {
        int? id;

        await using (var connection = await database.OpenConnectionAsync())
        {
            id = await connection.QuerySingleAsync<int?>(
                "SELECT get_work_id_by_slug(@Slug)",
                new { Slug = slug }
            );
        }

        return id is int workId ? await GetAsync(workId) : null;
    }

    // the search runs elsewhere and hands its matches in; null means nothing was searched for
    // excludedCollectionId leaves out the works already in that collection, for a page adding to it
    public async Task<WorkListPage> GetListAsync(
        WorkListFilter filter,
        IReadOnlyList<WorkId>? searchMatches,
        CollectionId? excludedCollectionId
    )
    {
        await using var connection = await database.OpenConnectionAsync();

        var pageSize = ArtistShopLimits.WorkListPageSize;

        int? collectionId = filter.Collection is WorkCollectionFilter.InCollection inCollection ? inCollection.CollectionId.Value : null;

        var rows = (
            await connection.QueryAsync<WorkListRow>(
                """
                SELECT * FROM get_work_list(
                    @WorkTypeIds, @VocabularyTermIds, @MatchingWorkIds, @IsSearching,
                    @CollectionId, @IsInNoCollection, @ExcludedCollectionId, @HasImages, @IsForSale, @Sort, @Offset, @PageSize
                )
                """,
                new
                {
                    WorkTypeIds = (int[])[.. filter.WorkTypeIds.Select(id => id.Value)],
                    VocabularyTermIds = (int[])[.. filter.VocabularyTermIds.Select(id => id.Value)],
                    MatchingWorkIds = (int[])[.. searchMatches?.Select(id => id.Value) ?? []],
                    IsSearching = searchMatches is not null,
                    CollectionId = collectionId,
                    IsInNoCollection = filter.Collection is WorkCollectionFilter.InNoCollection,
                    ExcludedCollectionId = excludedCollectionId?.Value,
                    filter.HasImages,
                    filter.IsForSale,
                    Sort = (short)filter.Sort,
                    Offset = (filter.PageNumber - 1) * pageSize,
                    PageSize = pageSize,
                }
            )
        ).ToList();

        var items = rows.Select(row => new WorkListItem(
                new WorkId(row.Id),
                new WorkName(row.Name),
                new WorkSlug(row.Slug),
                new WorkTypeName(row.WorkTypeName),
                row is { DateCreated: DateOnly date, DateCreatedPrecision: DatePrecision precision }
                    ? new PartialDate(date, precision)
                    : null,
                row.CollectionNames,
                row.ImageCount,
                row.IsForSale,
                row.PrimaryImage
            ))
            .ToList();

        // the count rides on the rows, so an empty page carries none; the page component
        // sends a page number past the end back to the first page
        var totalCount = rows.Count is 0 ? 0 : rows[0].TotalCount;

        return new WorkListPage(items, totalCount, filter.PageNumber, pageSize);
    }

    // the slug comes back because the function decides it: a rename can land on a numbered one
    public async Task<WorkSlug> UpdateAsync(WorkCatalogUpdate workCatalogUpdate)
    {
        var parameters = DetailsParameters(workCatalogUpdate.Details);
        parameters.Add(
            "Images",
            CreateImageInputs(workCatalogUpdate.Images, workCatalogUpdate.MainImageIndex)
        );

        return await ExecuteUpdateAsync(
            """
            SELECT update_work(
                @Id, @Name, @CandidateSlug, @Description, @DateCreated, @DateCreatedPrecision,
                @HeightCm, @WidthCm, @DepthCm, @DurationSeconds,
                @Images, @VocabularyTermIds, @CollectionIds
            )
            """,
            parameters
        );
    }

    // leaves the images alone, so uploads appending to the work meanwhile keep theirs
    public Task<WorkSlug> UpdateDetailsAsync(WorkDetailsUpdate details) =>
        ExecuteUpdateAsync(
            """
            SELECT update_work_details(
                @Id, @Name, @CandidateSlug, @Description, @DateCreated, @DateCreatedPrecision,
                @HeightCm, @WidthCm, @DepthCm, @DurationSeconds,
                @VocabularyTermIds, @CollectionIds
            )
            """,
            DetailsParameters(details)
        );

    private static DynamicParameters DetailsParameters(WorkDetailsUpdate details) =>
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
                CollectionIds = (int[])[.. details.CollectionIds.Select(id => id.Value)],
            }
        );

    private async Task<WorkSlug> ExecuteUpdateAsync(string sql, DynamicParameters parameters)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            return new WorkSlug(await connection.QuerySingleAsync<string>(sql, parameters));
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.WorkNoLongerExists))
        {
            throw new WorkDeletedException(exception.Message, exception);
        }
        catch (PostgresException exception) when (IsCatalogChanged(exception))
        {
            throw new ChangedSincePageLoadException(exception.Message, exception);
        }
    }

    // the images only, for the work table's images dialog, which saves them apart from the other fields
    public async Task UpdateImagesAsync(WorkId id, IReadOnlyList<WorkImage> images, int mainImageIndex)
    {
        await using var connection = await database.OpenConnectionAsync();

        try
        {
            await connection.ExecuteAsync(
                "SELECT update_work_images(@Id, @Images)",
                new { Id = id.Value, Images = CreateImageInputs(images, mainImageIndex) }
            );
        }
        catch (PostgresException exception)
            when (SqlErrors.IsThrown(exception, SqlStates.WorkNoLongerExists))
        {
            throw new WorkDeletedException(exception.Message, exception);
        }
    }

    public Task DeleteAsync(WorkId id) => DeleteManyAsync([id]);

    // the images, collection rows, term rows and products go with them, through the foreign keys'
    // ON DELETE CASCADE. The image files wait for OrphanedImageSweeper
    public async Task DeleteManyAsync(IReadOnlyCollection<WorkId> ids)
    {
        await using var connection = await database.OpenConnectionAsync();

        await connection.ExecuteAsync(
            "SELECT delete_works(@Ids)",
            new { Ids = (int[])[.. ids.Select(id => id.Value)] }
        );
    }

    private async Task<Work?> GetAsync(int id)
    {
        await using var connection = await database.OpenConnectionAsync();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_work(@Id);
            SELECT * FROM get_work_images(@Id);
            SELECT * FROM get_work_collections(@Id);
            SELECT * FROM get_work_vocabulary_terms(@Id);
            SELECT * FROM get_work_products(@Id);
            """,
            new { Id = id }
        );

        // These Read calls MUST run in the same order as the SELECTs above
        var row = await results.ReadSingleOrDefaultAsync<WorkRow>();

        if (row is null)
        {
            return null;
        }

        return ToWork(
            row,
            [.. await results.ReadAsync<ImageRow>()],
            [.. await results.ReadAsync<CollectionRow>()],
            [.. await results.ReadAsync<VocabularyTermRow>()],
            [.. await results.ReadAsync<ProductRow>()]
        );
    }

    // every work on the site with its images, collections, terms and products, for the site export
    public Task<List<Work>> GetAllAsync() => GetWorksAsync(workIds: null);

    // in id order, leaving out any deleted since the ids were read
    public Task<List<Work>> GetManyAsync(IReadOnlyCollection<WorkId> ids) =>
        GetWorksAsync([.. ids.Select(id => id.Value)]);

    // null for every work
    private async Task<List<Work>> GetWorksAsync(int[]? workIds)
    {
        await using var connection = await database.OpenConnectionAsync();

        await using var results = await connection.QueryMultipleAsync(
            """
            SELECT * FROM get_all_works(@WorkIds);
            SELECT * FROM get_all_work_images(@WorkIds);
            SELECT * FROM get_all_work_collections(@WorkIds);
            SELECT * FROM get_all_work_vocabulary_terms(@WorkIds);
            SELECT * FROM get_all_work_products(@WorkIds);
            """,
            new { WorkIds = workIds }
        );

        // These Read calls MUST run in the same order as the SELECTs above
        var rows = await results.ReadAsync<WorkRow>();
        var images = (await results.ReadAsync<ImageRowWithWorkId>()).ToLookup(image => image.WorkId);
        var collections = (await results.ReadAsync<CollectionRowWithWorkId>()).ToLookup(collections => collections.WorkId);
        var terms = (await results.ReadAsync<VocabularyTermRowWithWorkId>()).ToLookup(term => term.WorkId);
        var products = (await results.ReadAsync<ProductRowWithWorkId>()).ToLookup(product => product.WorkId);

        return
        [
            .. rows.Select(row =>
                ToWork(row, [.. images[row.Id]], [.. collections[row.Id]], [.. terms[row.Id]], [.. products[row.Id]])
            ),
        ];
    }

    private static Work ToWork(
        WorkRow row,
        List<ImageRow> images,
        List<CollectionRow> collectionRows,
        List<VocabularyTermRow> termRows,
        List<ProductRow> productRows
    )
    {
        var collections = collectionRows
            .Select(collectionRow => new Collection(
                new CollectionId(collectionRow.Id),
                new CollectionName(collectionRow.Name),
                new CollectionSlug(collectionRow.Slug)
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

        var work = new Work(
            new WorkId(row.Id),
            new WorkType(
                new WorkTypeId(row.WorkTypeId),
                new WorkTypeName(row.WorkTypeName)
            ),
            new WorkName(row.Name),
            new WorkSlug(row.Slug),
            row.Description,
            dateCreated,
            dimensions,
            duration,
            images.Select(image => new WorkImage(
                image.StorageKey,
                image.OriginalFileName,
                image.Width,
                image.Height,
                image.BlurDataUri
            )),
            collections,
            vocabularyTerms,
            products
        )
        {
            MainImageIndex = Math.Max(images.FindIndex(image => image.IsPrimary), 0),
        };

        return work;
    }

    // every column is nullable: a work at the very first or last place has no row to read there
    private sealed class WorkNeighboursRow
    {
        public string? PreviousCollectionSlug { get; init; }
        public string? PreviousWorkSlug { get; init; }
        public int? PreviousImageCount { get; init; }
        public string? NextCollectionSlug { get; init; }
        public string? NextWorkSlug { get; init; }
        public int? NextImageCount { get; init; }
    }

    private sealed class WorkWalkPositionRow
    {
        public required int EarlierImageCount { get; init; }
        public required int TotalImageCount { get; init; }
    }

    private sealed class AddedWorkRow
    {
        public required int Id { get; init; }
        public required string Slug { get; init; }
    }

    private sealed class WorkRow
    {
        public required int Id { get; init; }
        public required int WorkTypeId { get; init; }
        public required string WorkTypeName { get; init; }
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

    private sealed class ImageWithWorkRow
    {
        public required int WorkId { get; init; }
        public required string WorkName { get; init; }
        public required string WorkSlug { get; init; }
        public required string StorageKey { get; init; }
        public string? OriginalFileName { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public string? BlurDataUri { get; init; }
        public required int ImageNumber { get; init; }
    }

    private class CollectionRow
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

    // get_all_work_*'s rows, which say whose they are
    private sealed class ImageRowWithWorkId : ImageRow
    {
        public required int WorkId { get; init; }
    }

    private sealed class CollectionRowWithWorkId : CollectionRow
    {
        public required int WorkId { get; init; }
    }

    private sealed class VocabularyTermRowWithWorkId : VocabularyTermRow
    {
        public required int WorkId { get; init; }
    }

    private sealed class ProductRowWithWorkId : ProductRow
    {
        public required int WorkId { get; init; }
    }

    private sealed class WorkListRow
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Slug { get; init; }
        public required string WorkTypeName { get; init; }
        public required DateOnly? DateCreated { get; init; }
        public required DatePrecision? DateCreatedPrecision { get; init; }
        public required int ImageCount { get; init; }
        public required bool IsForSale { get; init; }
        public string? CollectionNames { get; init; }
        public string? PrimaryImageStorageKey { get; init; }
        public int? PrimaryImageWidth { get; init; }
        public int? PrimaryImageHeight { get; init; }
        public string? PrimaryImageBlurDataUri { get; init; }
        public required int TotalCount { get; init; }

        // a work with no image leaves all four image columns null, and matching all three
        // of the non-null ones together is what lets the width and height be read as plain ints
        public WorkImage? PrimaryImage =>
            this
                is {
                    PrimaryImageStorageKey: string storageKey,
                    PrimaryImageWidth: int width,
                    PrimaryImageHeight: int height,
                }
                ? new WorkImage(storageKey, null, width, height, PrimaryImageBlurDataUri)
                : null;
    }
}
