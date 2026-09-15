using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;

namespace ArtistShop.Web.Tests.Database;

public class CatalogTestData(SqlConnectionFactory connectionFactory)
{
    private readonly ShopItemTypeRepository _shopItemTypes = new(connectionFactory);
    private readonly VocabularyRepository _vocabularies = new(connectionFactory);
    private readonly VocabularyTermRepository _terms = new(connectionFactory);
    private readonly PaintingRepository _paintings = new(connectionFactory);
    private readonly SeriesRepository _series = new(connectionFactory);

    public async Task<ShopItemTypeId> GetPaintingTypeIdAsync() =>
        (await _shopItemTypes.GetAllAsync()).Single(type => type.Name.Value == "Painting").Id;

    public async Task<VocabularyId> AddPaintingVocabularyAsync() =>
        await _vocabularies.AddAsync(
            new VocabularyName($"Medium {Guid.NewGuid():n}"),
            [await GetPaintingTypeIdAsync()]
        );

    public Task<VocabularyTermId> AddTermAsync(VocabularyId vocabularyId) =>
        _terms.AddAsync(vocabularyId, new VocabularyTermName($"Oil {Guid.NewGuid():n}"));

    public Task<SeriesId> AddSeriesAsync()
    {
        var name = $"Series {Guid.NewGuid():n}";
        return _series.AddAsync(new SeriesName(name), SeriesSlug.FromName(name));
    }

    // the path only has to be unique: nothing reads the file
    public static ShopItemImage CreateTestImage() =>
        new($"test/{Guid.NewGuid():n}", OriginalFileName: null, 800, 600, BlurDataUri: null);

    public async Task<ShopItemSlug> AddPaintingWithTermAsync(VocabularyTermId termId) =>
        (
            await AddPaintingAsync(
                $"Vocabulary test painting {Guid.NewGuid():n}",
                termIds: [termId],
                seriesIds: [],
                images: []
            )
        ).Slug;

    public async Task<ShopItemId> AddPaintingInSeriesAsync(
        SeriesId seriesId,
        IReadOnlyList<ShopItemImage> images
    ) =>
        (
            await AddPaintingAsync(
                $"Series test painting {Guid.NewGuid():n}",
                termIds: [],
                seriesIds: [seriesId],
                images
            )
        ).Id;

    public async Task<ShopItemIdentifiers> AddPaintingAsync(
        string name,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<SeriesId> seriesIds,
        IReadOnlyList<ShopItemImage> images
    )
    {
        var identifiers = await _paintings.AddAsync(
            new PaintingCatalogAddition(
                new ShopItemName(name),
                ShopItemSlug.FromName(name),
                Price: null,
                Stock: 1,
                DatePainted: null,
                Description: null,
                Dimensions: null,
                Images: images,
                MainImageIndex: 0,
                VocabularyTermIds: termIds,
                SeriesIds: seriesIds
            )
        );

        return identifiers;
    }
}
