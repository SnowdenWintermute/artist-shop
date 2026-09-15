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

    public async Task<ShopItemTypeId> GetPaintingTypeIdAsync() =>
        (await _shopItemTypes.GetAllAsync()).Single(type => type.Name.Value == "Painting").Id;

    public async Task<VocabularyId> AddPaintingVocabularyAsync() =>
        await _vocabularies.AddAsync(
            new VocabularyName($"Medium {Guid.NewGuid():n}"),
            [await GetPaintingTypeIdAsync()]
        );

    public Task<VocabularyTermId> AddTermAsync(VocabularyId vocabularyId) =>
        _terms.AddAsync(vocabularyId, new VocabularyTermName($"Oil {Guid.NewGuid():n}"));

    public async Task<ShopItemSlug> AddPaintingWithTermAsync(VocabularyTermId termId)
    {
        var name = $"Vocabulary test painting {Guid.NewGuid():n}";
        var identifiers = await _paintings.AddAsync(
            new PaintingCatalogAddition(
                new ShopItemName(name),
                ShopItemSlug.FromName(name),
                Price: null,
                Stock: 1,
                DatePainted: null,
                Description: null,
                Dimensions: null,
                Images: [],
                MainImageIndex: 0,
                VocabularyTermIds: [termId],
                SeriesIds: []
            )
        );

        return identifiers.Slug;
    }
}
