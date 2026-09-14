using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Database;

public sealed class VocabularyRepositoryTests(TestDatabaseFixture database)
{
    private readonly VocabularyRepository _vocabularies = new(database.ConnectionFactory);
    private readonly ShopItemTypeRepository _shopItemTypes = new(database.ConnectionFactory);

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
        var painting = (await _shopItemTypes.GetAllAsync()).Single(type =>
            type.Name.Value == "Painting"
        );

        var id = await _vocabularies.AddAsync(name, [painting.Id]);

        Assert.Contains(new Vocabulary(id, name), await _vocabularies.GetAllAsync());
    }

    [Fact]
    public async Task RejectsDuplicateName()
    {
        var name = UniqueName();
        await _vocabularies.AddAsync(name, []);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() => _vocabularies.AddAsync(name, []));
    }
}
