using System.Security.Cryptography;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using Npgsql;

namespace ArtistShop.Web.Tests.Database;

public class CatalogTestData(SiteDatabase database)
{
    private readonly WorkTypeRepository _workTypes = new(database);
    private readonly VocabularyRepository _vocabularies = new(database);
    private readonly VocabularyTermRepository _terms = new(database);
    private readonly WorkRepository _works = new(database);
    private readonly CollectionRepository _collections = new(database);

    public async Task<WorkTypeId> GetTypeIdAsync(string name) =>
        (await _workTypes.GetAllAsync()).Single(type => type.Name.Value == name).Id;

    public Task<WorkTypeId> GetPaintingTypeIdAsync() => GetTypeIdAsync("Painting");

    public async Task<WorkIdentifiers> AddWorkWithDimensionsAsync(
        WorkTypeId typeId,
        DimensionsCentimeters dimensions
    )
    {
        var name = $"Dimensions test {Guid.NewGuid():n}";

        return await _works.AddAsync(
            new WorkCatalogAddition(
                typeId,
                new WorkName(name),
                WorkSlug.FromName(name),
                Description: null,
                DateCreated: null,
                Dimensions: dimensions,
                Duration: null,
                Images: [],
                MainImageIndex: 0,
                VocabularyTermIds: [],
                CollectionIds: [],
                NewCollectionNames: [],
                Products: []
            )
        );
    }

    public Task<WorkIdentifiers> AddWorkAsync(WorkTypeId typeId, string name) =>
        _works.AddAsync(
            CreateWorkAddition(
                typeId,
                name,
                termIds: [],
                collectionIds: [],
                images: [],
                products: [],
                duration: null
            )
        );

    public async Task<VocabularyId> AddPaintingVocabularyAsync() =>
        await _vocabularies.AddAsync(
            new VocabularyName($"Medium {Guid.NewGuid():n}"),
            isMutuallyExclusive: false,
            [await GetPaintingTypeIdAsync()]
        );

    public Task<VocabularyTermId> AddTermAsync(VocabularyId vocabularyId) =>
        _terms.AddAsync(vocabularyId, new VocabularyTermName($"Oil {Guid.NewGuid():n}"));

    public Task<CollectionId> AddCollectionAsync()
    {
        var name = $"Collection {Guid.NewGuid():n}";
        return _collections.AddAsync(new CollectionName(name), CollectionSlug.FromName(name));
    }

    // nothing reads the file, but the key has to be shaped like a real one: 32 hexadecimal
    // characters, which is what the char(32) column holds
    public static WorkImage CreateTestImage() =>
        new($"{Guid.NewGuid():n}", OriginalFileName: null, 800, 600, BlurDataUri: null);

    // a hash no other test image has
    public static string UniqueSha256() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public async Task<WorkSlug> AddPaintingWithTermAsync(VocabularyTermId termId) =>
        (
            await AddPaintingAsync(
                $"Vocabulary test painting {Guid.NewGuid():n}",
                termIds: [termId],
                collectionIds: [],
                images: []
            )
        ).Slug;

    public Task<WorkIdentifiers> AddPaintingInCollectionAsync(
        CollectionId collectionId,
        IReadOnlyList<WorkImage> images
    ) =>
        AddPaintingAsync(
            $"Collection test painting {Guid.NewGuid():n}",
            termIds: [],
            collectionIds: [collectionId],
            images
        );

    public Task<WorkIdentifiers> AddPaintingAsync(
        string name,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<CollectionId> collectionIds,
        IReadOnlyList<WorkImage> images
    ) => AddPaintingAsync(name, termIds, collectionIds, images, products: [], duration: null);

    public async Task<WorkIdentifiers> AddPaintingAsync(
        string name,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<CollectionId> collectionIds,
        IReadOnlyList<WorkImage> images,
        IReadOnlyList<ProductAddition> products,
        TimeSpan? duration
    )
    {
        return await _works.AddAsync(
            CreateWorkAddition(
                await GetPaintingTypeIdAsync(),
                name,
                termIds,
                collectionIds,
                images,
                products,
                duration
            )
        );
    }

    public static WorkCatalogAddition CreateWorkAddition(
        WorkTypeId typeId,
        string name,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<CollectionId> collectionIds,
        IReadOnlyList<WorkImage> images,
        IReadOnlyList<ProductAddition> products,
        TimeSpan? duration
    ) =>
        new(
            typeId,
            new WorkName(name),
            WorkSlug.FromName(name),
            Description: null,
            DateCreated: null,
            Dimensions: null,
            Duration: duration,
            Images: images,
            MainImageIndex: 0,
            VocabularyTermIds: termIds,
            CollectionIds: collectionIds,
            NewCollectionNames: [],
            Products: products
        );
}
