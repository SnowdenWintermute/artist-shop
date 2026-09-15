using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using Dapper;

namespace ArtistShop.Web.Tests.Database;

public sealed class VocabularyRepositoryTests(TestDatabaseFixture database)
{
    private readonly VocabularyRepository _vocabularies = new(database.ConnectionFactory);
    private readonly ShopItemTypeRepository _shopItemTypes = new(database.ConnectionFactory);
    private readonly PaintingRepository _paintings = new(database.ConnectionFactory);

    // test classes share the database in parallel, so names must not collide
    private static VocabularyName UniqueName() => new($"Medium {Guid.NewGuid():n}");

    [Fact]
    public async Task ListsTheSeededPaintingType()
    {
        var shopItemTypes = await _shopItemTypes.GetAllAsync();

        Assert.Contains(shopItemTypes, shopItemType => shopItemType.Name.Value == "Painting");
    }

    [Fact]
    public async Task AddedVocabularyAppearsInTheList()
    {
        var name = UniqueName();

        var id = await _vocabularies.AddAsync(name, [await GetPaintingTypeIdAsync()]);

        Assert.Contains(new Vocabulary(id, name), await _vocabularies.GetAllAsync());
    }

    [Fact]
    public async Task RejectsDuplicateName()
    {
        var name = UniqueName();
        await _vocabularies.AddAsync(name, []);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => _vocabularies.AddAsync(name, []));
    }

    [Fact]
    public async Task GetReturnsVocabularNameAndAssociatedShopItemTypes()
    {
        var name = UniqueName();
        var paintingTypeId = await GetPaintingTypeIdAsync();
        var id = await _vocabularies.AddAsync(name, [paintingTypeId]);

        var vocabulary = await _vocabularies.GetAsync(id);

        Assert.NotNull(vocabulary);
        Assert.Equal(name, vocabulary.Name);
        Assert.Equal(paintingTypeId, Assert.Single(vocabulary.ShopItemTypeIds));
    }

    [Fact]
    public async Task GetReturnsNullForUnknownId()
    {
        // ids start at 1
        Assert.Null(await _vocabularies.GetAsync(new VocabularyId(0)));
    }

    [Fact]
    public async Task UpdateRenames()
    {
        var paintingTypeId = await GetPaintingTypeIdAsync();
        var id = await _vocabularies.AddAsync(UniqueName(), [paintingTypeId]);
        var newName = UniqueName();

        await _vocabularies.UpdateAsync(id, newName, [paintingTypeId]);

        var vocabulary = await _vocabularies.GetAsync(id);
        Assert.NotNull(vocabulary);
        Assert.Equal(newName, vocabulary.Name);
    }

    [Fact]
    public async Task UpdateRejectsDuplicateName()
    {
        var takenName = UniqueName();
        await _vocabularies.AddAsync(takenName, []);
        var id = await _vocabularies.AddAsync(UniqueName(), []);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _vocabularies.UpdateAsync(id, takenName, [])
        );
    }

    [Fact]
    public async Task UntickingShopItemTypeRemovesVocabularyTermsFromItsItemsButKeepsTerms()
    {
        var name = UniqueName();
        var paintingTypeId = await GetPaintingTypeIdAsync();
        var id = await _vocabularies.AddAsync(name, [paintingTypeId]);
        await AddPaintingWithTermAsync(await AddTermAsync(id));
        Assert.Equal(1, (await _vocabularies.CountUsageAsync(id)).ShopItemCountFor(paintingTypeId));

        await _vocabularies.UpdateAsync(id, name, []);

        var vocabulary = await _vocabularies.GetAsync(id);
        Assert.NotNull(vocabulary);
        Assert.Empty(vocabulary.ShopItemTypeIds);
        var usage = await _vocabularies.CountUsageAsync(id);
        Assert.Equal(0, usage.ShopItemCountFor(paintingTypeId));
        Assert.Equal(1, usage.VocabularyTermCount);
    }

    [Fact]
    public async Task DeleteRemovesVocabularyAndKeepsShopItems()
    {
        var id = await _vocabularies.AddAsync(UniqueName(), [await GetPaintingTypeIdAsync()]);
        var slug = await AddPaintingWithTermAsync(await AddTermAsync(id));

        await _vocabularies.DeleteAsync(id);

        Assert.Null(await _vocabularies.GetAsync(id));
        var painting = await _paintings.GetBySlugAsync(slug.Value);
        Assert.NotNull(painting);
        Assert.Empty(painting.VocabularyTerms);
    }

    private async Task<ShopItemTypeId> GetPaintingTypeIdAsync() =>
        (await _shopItemTypes.GetAllAsync()).Single(type => type.Name.Value == "Painting").Id;

    // until VocabularyTermRepository exists (step 3)
    private async Task<VocabularyTermId> AddTermAsync(VocabularyId vocabularyId)
    {
        await using var connection = database.ConnectionFactory.Create();

        // OUTPUT INSERTED.Id returns the new row's id from the INSERT itself
        var termId = await connection.QuerySingleAsync<int>(
            "INSERT INTO dbo.VocabularyTerms (VocabularyId, Name) OUTPUT INSERTED.Id VALUES (@VocabularyId, N'Oil')",
            new { VocabularyId = vocabularyId.Value }
        );
        return new VocabularyTermId(termId);
    }

    private async Task<ShopItemSlug> AddPaintingWithTermAsync(VocabularyTermId termId)
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
